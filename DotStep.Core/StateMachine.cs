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

            var stateMachineName = GetType().Name;

            var template = new
            {
                Parameters = new {
                    CodeS3Bucket = new {
                        Type = "String"
                    },
                    CodeS3Key = new {
                        Type = "String"
                    }
                },
                Resources = new Dictionary<string, object>()
            };


            var lambdaNames = new List<String>();

            foreach (var state in States.Where(s => s is ITaskState))
            {
                var lambdaName = $"{stateMachineName}{state.Name}";
                lambdaNames.Add(lambdaName);
                var assemblyName = GetType().GetTypeInfo().Assembly.GetName().Name;
                var namespaceName = GetType().GetTypeInfo().Namespace;
                
                var handler = $"{assemblyName}::{namespaceName}.{state.Name}::Execute";

               // var lambdaRoleName = state.GetAttributeValue((ExplicitRole a) => a.RoleName, DefaultLambdaRoleName);
                var memory = state.GetAttributeValue((FunctionMemory a) => a.Memory, DefaultMemory);
                var timeout = state.GetAttributeValue((FunctionTimeout a) => a.Timeout, DefaultTimeout);

                var assembly = Assembly.Load(new AssemblyName(assemblyName));
                var assemblyDefinition = AssemblyDefinition.ReadAssembly(assembly.Location);
                var type = assemblyDefinition.MainModule.Types.FirstOrDefault(t => t.Name == state.GetType().Name);
                var calls = type.Methods.First(x => x.Name == "Execute").Body
                    .Instructions.Where(x => x.OpCode == OpCodes.Call)
                    .Select(x => x.Operand);

           

                var actions = new List<string>();

                foreach (var call in calls) {
                    if (call.GetType().GetProperty("GenericArguments") != null) {
                        var amazon = new List<string>();
                        var arguments = (call as dynamic).GenericArguments;
                        foreach (var field in arguments[0].Fields)
                        {
                            string fieldName = field.FieldType.FullName;
                            if (fieldName.StartsWith("Amazon") && fieldName.Contains("Response"))
                            {
                                if (!amazon.Contains(fieldName))
                                {
                                    amazon.Add(fieldName);

                                    var service = fieldName.Split('.')[1];
                                    var method = fieldName.Split('.')[3].Replace("Response", string.Empty);

                                    var iamNamespace = DotStepUtil.GetIAMNamespace(service);

                                    actions.Add($"{iamNamespace}:{method}");
                                }

                            }
                        }                        
                    }
                    
                }

  

                var lambdaRoleName = $"{lambdaName}Role";
                var lambdaRole = new
                {
                    Type = "AWS::IAM::Role",
                    Properties = new
                    {
                        AssumeRolePolicyDocument = new
                        {
                            Version = "2012-10-17",
                            Statement = new
                            {
                                Effect = "Allow",
                                Principal = new
                                {
                                    Service = "lambda.amazonaws.com"
                                },
                                Action = "sts:AssumeRole"
                            }
                        },
                        Policies = new List<dynamic> {
                            new {
                                PolicyName = $"{lambdaName}Policy",
                                PolicyDocument = new
                                {
                                    Version = "2012-10-17",
                                    Statement = new
                                    {
                                        Effect = actions.Any() ? "Allow" : "Deny",
                                        Resource = "*",
                                        Action = actions.Any() ? actions : new List<string>{"*" }
                                    }
                                }
                            }
                        }
                    }
                };

                template.Resources.Add(lambdaRoleName, lambdaRole);
     
                var functionResource = new
                {
                    Type = "AWS::Lambda::Function",
                    Properties = new {
                        Runtime = Runtime.Dotnetcore10.Value,
                        Handler = handler,
                        Timeout = timeout,
                        MemorySize = memory,
                        Code = new {
                            S3Bucket = new { Ref = "CodeS3Bucket" },
                            S3Key = new {Ref="CodeS3Key"}
                        },
                        Role = new Dictionary<string, List<string>> {
                            { "Fn::GetAtt", new List<string>{
                                lambdaRoleName, "Arn"
                            } }
                        }
                    }
                };

                template.Resources.Add(lambdaName, functionResource);                

            }

            

            var stateMachineRoleName = $"{stateMachineName}Role";
            var stateMachineRole = new
            {
                Type = "AWS::IAM::Role",
                Properties = new
                {
                    AssumeRolePolicyDocument = new
                    {
                        Version = "2012-10-17",
                        Statement = new
                        {
                            Effect = "Allow",
                            Principal = new
                            {
                                Service = new Dictionary<string, string> {
                                    { "Fn::Sub", "states.${AWS::Region}.amazonaws.com" }
                                }
                            },
                            Action = "sts:AssumeRole"
                        }
                    },
                    Policies = new List<dynamic> {
                        new {
                            PolicyName = $"{stateMachineName}Policy",
                            PolicyDocument = new
                                {
                                    Version = "2012-10-17",
                                    Statement = new
                                    {
                                        Effect = "Allow",
                                        Resource = lambdaNames.Select(n =>
                                            new Dictionary<string, List<string>> {
                                                {
                                                    "Fn::GetAtt", new List<string>{
                                                                        n, "Arn"
                                                                    }
                                                }
                                            }),
                                        Action = "lambda:InvokeFunction"
                                    }
                                }
                        }
                    }                    
                }
            };
            template.Resources.Add(stateMachineRoleName, stateMachineRole);

            var definition = Describe("${AWS::AccountId}", "${AWS::Region}");

            var stateMachineResource = new
            {
                Type = "AWS::StepFunctions::StateMachine",
                Properties = new {
                    //RoleArn = new { Ref = stateMachineRoleName },
                    DefinitionString = new Dictionary<string, string> { { "Fn::Sub", definition } },
                        RoleArn = new Dictionary<string, List<string>> {
                            { "Fn::GetAtt", new List<string>{
                                stateMachineRoleName, "Arn"
                            } }
                        }
                }

            };

            template.Resources.Add(stateMachineName, stateMachineResource);
            
            var json = JsonConvert.SerializeObject(template, Formatting.Indented);

            Console.Write(json);

            try
            {
           var validateResult = await cloudFormation.ValidateTemplateAsync(new ValidateTemplateRequest
            {
                TemplateBody = json
            });
            }
            catch (Exception e) {
                Console.Write(e.Message);
            }
 

            

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
