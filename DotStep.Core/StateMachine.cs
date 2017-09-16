using Amazon.Lambda;
using Amazon.Lambda.Model;
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

            return sb.ToString();

        }

        public async Task PublishAsync(
            string region, 
            string accountId, 
            string roleName,
            string publishLocation)
        {
            var statMachineName = GetType().Name;

            // Build the code
            var buildProcess = Process.Start(new ProcessStartInfo("dotnet", "publish")
            {

            });

            while (!buildProcess.HasExited)
            {
                Console.WriteLine("Waiting for dotnet publish to complete. " + DateTime.UtcNow.ToString());
                Thread.Sleep(250);
            }

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
                var lambdaName = $"{statMachineName}.{state.Name}";
                Console.WriteLine("Creating function: " + lambdaName);

                using (var codeStream = new MemoryStream())
                {
                    File.Open(fileLocation, FileMode.Open).CopyTo(codeStream);

                    IAmazonLambda lambda = new AmazonLambdaClient();

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
                        // this is where we have to create it..

                        Console.WriteLine("Function not found, creating now...");

                        var assemblyName = GetType().GetTypeInfo().Assembly.GetName().Name;
                        var namespaceName = GetType().GetTypeInfo().Namespace;
                        var className = GetType().Name;
                        var handler = $"{assemblyName}::{namespaceName}.{className}::Execute";

                        var createFunctionResult = await lambda.CreateFunctionAsync(new CreateFunctionRequest
                        {
                            Runtime = Runtime.Dotnetcore10,
                            FunctionName = lambdaName,
                            Handler = handler,
                            Role = $"arn:aws:iam::{accountId}:role/{roleName}",
                            Timeout = 30,
                            MemorySize = 512,
                            Code = new FunctionCode
                            {
                                ZipFile = codeStream
                            }
                        });
                    }
                }
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
