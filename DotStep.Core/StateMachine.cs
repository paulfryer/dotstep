using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using Amazon.Lambda;
using Amazon.Lambda.Model;
using Amazon.Runtime;
using Amazon.StepFunctions;
using Amazon.StepFunctions.Model;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DotStep.Core
{

    public abstract class StateMachine<TStartsAt> : IStateMachine 
        where TStartsAt : IState
    {
        public Type StartAt {
            get { 
                return this.GetType().GetTypeInfo().BaseType.GetGenericArguments()[0]; 
            }
        }

        private IEnumerable<IState> States
        {
            get
            {

                return GetType().GetTypeInfo().Assembly.GetTypes()
                                     .Where(t => typeof(IState).IsAssignableFrom(t) &&
                                            t.GetTypeInfo().IsClass &&
                                            t.GetTypeInfo().IsSealed &&
                                            t.Namespace == StartAt.Namespace)
                                         .Select(t => (IState)Activator.CreateInstance(t));
            }
        }



        public async Task<string> BuildCloudFormationTemplate()
        {
            IAmazonCloudFormation cloudFormation = new AmazonCloudFormationClient();

            var statMachineName = GetType().Name;
            var resources = new List<dynamic>();

            foreach (var state in States.Where(s => s is ITaskState))
            {
                var lambdaName = $"{statMachineName}-{state.Name}";

                var assemblyName = GetType().GetTypeInfo().Assembly.GetName().Name;
                var namespaceName = GetType().GetTypeInfo().Namespace;

                var assembly = Assembly.Load(new AssemblyName(assemblyName));

                var handler = $"{assemblyName}::{namespaceName}.{state.Name}::Execute";

                var lambdaRoleName = state.GetAttributeValue((ExplicitRole a) => a.RoleName, DefaultLambdaRoleName);
                var memory = state.GetAttributeValue((FunctionMemory a) => a.Memory, DefaultMemory);
                var timeout = state.GetAttributeValue((FunctionTimeout a) => a.Timeout, DefaultTimeout);

                //var x= new System.Reflection.Emit.DynamicMethod("", null, null);
                //var g = x.GetILGenerator();




                var callAssemblyName = typeof(IAmazonStepFunctions).GetTypeInfo().Assembly.GetName().Name;

                var callAssembly = AssemblyDefinition.ReadAssembly(
                    Assembly.Load(new AssemblyName(callAssemblyName)).Location);
                var callMethod = (MethodDefinition)callAssembly.MainModule.Types.First(t => t.Name == "IAmazonStepFunctions")
                    .Methods.First(m => m.Name == "ListStateMachinesAsync");


                var assemblyDefinition = AssemblyDefinition.ReadAssembly(assembly.Location);
                var type = assemblyDefinition.MainModule.Types.FirstOrDefault(t => t.Name == state.GetType().Name);
                var calls = type.Methods.First(x => x.Name == "Execute").Body
                    .Instructions.Where(x => x.OpCode == OpCodes.Call)
                    .Select(x => x.Operand);

                foreach (var call in calls) {
                    if (call.GetType().GetProperty("GenericArguments") != null) {
                        var amazon = new List<string>();
                        var arguments = (call as dynamic).GenericArguments;
                        foreach (var field in arguments[0].Fields)
                        {
                            string fieldName = field.FieldType.FullName;
                            if (fieldName.StartsWith("Amazon"))
                            {
                                if (!amazon.Contains(fieldName))
                                {
                                    amazon.Add(fieldName);

                                    var service = fieldName.Split('.')[1];
                                    var method = fieldName.Split('.')[3].Replace("Response", string.Empty);

                                    Console.WriteLine($"{service}:{method}");
                                }

                            }
                        }                        
                    }
                    
                }
               

                var functionResource = new
                {
                    Runtime = Runtime.Dotnetcore10,
                    FunctionName = lambdaName,
                    Handler = handler,
                    //Role = $"arn:aws:iam::{accountId}:role/{lambdaRoleName}",
                    Timeout = timeout,
                    MemorySize = memory,
                    Code = new FunctionCode
                    {
                        
                    }
                };
            }

            // TODO: Build Role for lambda function, need to implement a lookup table for Service name to IAM service name.

            var json = JsonConvert.SerializeObject(resources);


            var validateResult = await cloudFormation.ValidateTemplateAsync(new ValidateTemplateRequest
            {
                TemplateBody = json
            });

            return json;
        }

        public string Describe(string region, string accountId)
        {
            

            var sb = new StringBuilder();

            sb.AppendLine("{");
            sb.AppendLine("\"StartAt\": \"" + StartAt.Name + "\",");
            sb.AppendLine("\"States\": {");

            
            var appendComma = false;
            foreach (var state in States){
                if (appendComma) sb.Append(",");
                DescribeState(sb, state, region, accountId);
                appendComma = true;
            }
   
            sb.Append("}");
            sb.AppendLine("}");

            var rawJson = sb.ToString();

            var anonObject = JsonConvert.DeserializeObject(rawJson);
            var formattedJson = JsonConvert.SerializeObject(anonObject, Formatting.Indented);

            return formattedJson;
        }

        public Task PublishAsync(AWSCredentials awsCredentials, string region, string accountId)
        {
            return PublishAsync(awsCredentials, region, accountId, DefaultCodeLocation);
        }

        public async Task PublishAsync(
            AWSCredentials awsCredentials,
            string region, 
            string accountId,
            string publishLocation)
        {
            IAmazonLambda lambda = new AmazonLambdaClient(awsCredentials);
            IAmazonStepFunctions stepFunctions = new AmazonStepFunctionsClient(awsCredentials);

            var statMachineName = GetType().Name;
            /*
            // Build the code
            var buildProcess = Process.Start(new ProcessStartInfo("dotnet", "publish")
            {

            });

            while (!buildProcess.HasExited)
            {
                Console.WriteLine("Waiting for dotnet publish to complete. " + DateTime.UtcNow.ToString());
                Thread.Sleep(250);
            }
            */
            if (!Directory.Exists("deployment"))
                Directory.CreateDirectory("deployment");

            // Zip the code.
            var fileLocation = "deployment/" + statMachineName + ".zip";
            if (File.Exists(fileLocation))
                File.Delete(fileLocation);

            ZipFile.CreateFromDirectory(publishLocation, fileLocation);

            var statMachineDefinitionJson = Describe(region, accountId);

            foreach (var state in States.Where(s => s is ITaskState))
            {
                var lambdaName = $"{statMachineName}-{state.Name}";
                Console.WriteLine("Creating function: " + lambdaName);

                using (var codeStream = new MemoryStream())
                {
                    File.Open(fileLocation, FileMode.Open).CopyTo(codeStream);       
                    Console.WriteLine($"Processing Lambda for region: {region}.");

                    try
                    {
                        var getFunctionResult = await lambda.GetFunctionAsync(new GetFunctionRequest
                        {
                            FunctionName = lambdaName
                        });

                        Console.WriteLine($"Updating function: {lambdaName}");

                        var updateFunctionResult = await lambda.UpdateFunctionCodeAsync(new UpdateFunctionCodeRequest
                        {
                            FunctionName = lambdaName,
                            Publish = true,
                            ZipFile = codeStream
                        });

                    }
                    catch (ResourceNotFoundException)
                    {
                        try
                        {
                            Console.WriteLine("Function not found, creating now...");

                            var assemblyName = GetType().GetTypeInfo().Assembly.GetName().Name;
                            var namespaceName = GetType().GetTypeInfo().Namespace;
                            var handler = $"{assemblyName}::{namespaceName}.{state.Name}::Execute";

                            var lambdaRoleName = state.GetAttributeValue((ExplicitRole a) => a.RoleName, DefaultLambdaRoleName);
                            var memory = state.GetAttributeValue((FunctionMemory a) => a.Memory, DefaultMemory);
                            var timeout = state.GetAttributeValue((FunctionTimeout a) => a.Timeout, DefaultTimeout);
                            
                            var createFunctionResult = await lambda.CreateFunctionAsync(new CreateFunctionRequest
                            {
                                Runtime = Runtime.Dotnetcore10,
                                FunctionName = lambdaName,
                                Handler = handler,
                                Role = $"arn:aws:iam::{accountId}:role/{lambdaRoleName}",
                                Timeout = timeout,
                                MemorySize = memory,
                                Code = new FunctionCode
                                {
                                    ZipFile = codeStream
                                }
                            });
                        }
                        catch (Exception ex)
                        {
                            Console.Write(ex);
                        }

                    }
                    catch (Exception ex) {
                        Console.Write(ex);
                    }
                }
            }

            var stateMachineRoleName = this.GetAttributeValue((ExplicitRole a) => a.RoleName, DefaultStatesExecutionRoleName.Replace("{region}", region));
            var stateMachineRoleArn = $"arn:aws:iam::{accountId}:role/service-role/{stateMachineRoleName}";
            var definition = Describe(region, accountId);
            var stateMachineName = GetType().Name;
            var version = 1;
            var publishStateMachine = true;

            var listResult = await stepFunctions.ListStateMachinesAsync(new ListStateMachinesRequest
            {
                MaxResults = 1000
            });
            var latestVersion = listResult.StateMachines
                                .Where(sm => sm.StateMachineArn.Contains(stateMachineName))                
                                .Select(sm => sm.StateMachineArn)
                                .OrderByDescending(arn => arn)
                                .FirstOrDefault();

            if (latestVersion != null)
            {
                Console.WriteLine("Existsing version found: " + latestVersion);
                version = Convert.ToInt16(latestVersion.Substring(latestVersion.Length - 3));
                var descriptionResult = await stepFunctions.DescribeStateMachineAsync(new DescribeStateMachineRequest
                {
                    StateMachineArn = latestVersion
                });

                if (descriptionResult.Definition != definition)
                {
                    Console.WriteLine("Step function definintion has changed, creating new version.");
                    version++;
                }
                else publishStateMachine = false;    
            }

            if (publishStateMachine) {
                var paddedVersion = version.ToString().PadLeft(3, '0');
                stateMachineName += $"-v{ paddedVersion}";

                var createStateMachineResult = await stepFunctions.CreateStateMachineAsync(new CreateStateMachineRequest
                {
                    Definition = definition,
                    Name = stateMachineName,
                    RoleArn = stateMachineRoleArn
                });
                Console.Write(createStateMachineResult.StateMachineArn);
            }
        }

        public const string DefaultLambdaRoleName = "lambda_basic_execution";
        public const string DefaultStatesExecutionRoleName = "StatesExecutionRole-{region}";
        public const int DefaultTimeout = 30;
        public const int DefaultMemory = 128;
        public string DefaultCodeLocation
        {
            get
            {
                return $@"{Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)}\publish";
            }
        }
        
        void DescribeState(StringBuilder sb, IState state, string region, string accountId)
        {
            sb.AppendLine("\"" + state.GetType().Name + "\" : { ");

            if (state is ITaskState)
            {
                var taskState = state as ITaskState;
                sb.AppendLine("\"Type\":\"Task\",");
                sb.AppendLine($"\"Resource\":\"arn:aws:lambda:{region}:{accountId}:function:{GetType().Name}-{state.Name}\",");
                sb.AppendLine($"\"Next\":\"{taskState.Next.Name}\"");
            }
            if (state is IChoiceState){
                var choiceState = state as IChoiceState;
                sb.AppendLine("\"Type\":\"Choice\",");
                sb.AppendLine("\"Choices\": [");
                var appendComma = false;
                foreach(var choice in choiceState.Choices){
                    if (appendComma) sb.Append(",");
                    sb.AppendLine("{");
                    sb.AppendLine("\"Variable\":\"$." + choice.Variable + "\",");
                    var stringValue = Convert.ToString(choice.Value);
                    if (choice.Operator.ToUpper().StartsWith("ST"))
                        stringValue = "\"" + stringValue + "\"";
                    if (choice.Operator.ToUpper().StartsWith("BO"))
                        stringValue = stringValue.ToLower();
                    sb.AppendLine($"\"{choice.Operator}\": {stringValue},");
                    sb.AppendLine($"\"Next\":\"{choice.Next.Name}\"");
                    sb.AppendLine("}");
                    appendComma = true;
                }
                sb.AppendLine("]");
                if (choiceState.Default != null) {
                    sb.Append(",");
                    sb.AppendLine($"\"Default\": \"{choiceState.Default.Name}\"");
                }
            }
            if (state is IPassState){
                sb.AppendLine("\"Type\":\"Pass\",");
            }
            if (state is IWaitState){
                var waitState = state as IWaitState;
                sb.AppendLine("\"Type\":\"Wait\",");
                sb.AppendLine("\"Seconds\": " + waitState.Seconds + ",");
                sb.AppendLine($"\"Next\":\"{waitState.Next.Name}\"");

            }

            if (state.End)
                sb.AppendLine("\"End\": true");

            sb.AppendLine("}");
        }
    }


}
