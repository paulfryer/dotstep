using Amazon.IdentityManagement;
using Amazon.IdentityManagement.Model;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.StepFunctions;
using Amazon.StepFunctions.Model;
using DotStep.Core;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DotStep.StateMachines.StepFunctionDeployment
{

    public sealed class StepFunctionDeployer : StateMachine<GetStateMachinesToDeploy>
    {
    }

    public class Context : IContext
    {
        public string CodeS3Bucket { get; set; }
        public string CodeS3Key { get; set; }

        public Dictionary<string, string> StateMachinesToDeploy { get; internal set; }
        public int StateMachinesLeftToDeploy { get; internal set; }
    }

    public sealed class GetStateMachinesToDeploy : TaskState<Context, CheckForStateMachines>
    {
        IAmazonS3 s3 = new AmazonS3Client();

        public override async Task<Context> Execute(Context context)
        {
            if (string.IsNullOrEmpty(context.CodeS3Bucket))
                throw new Exception("CodeS3Bucket is required.");
            if (string.IsNullOrEmpty(context.CodeS3Key))
                throw new Exception("CodeS3Key is required.");

            context.StateMachinesToDeploy = new Dictionary<string, string>();

            var getObjectResult = await s3.GetObjectAsync(new GetObjectRequest
            {
                BucketName = context.CodeS3Bucket,
                Key = context.CodeS3Key
            });

            var assemblies = await getObjectResult.GetAssemblyNames(context);

            foreach (var assemblyFileName in assemblies)
            {
                var path = Path.Combine(Directory.GetCurrentDirectory(), assemblyFileName);
                var assemblyName = System.Runtime.Loader.AssemblyLoadContext.GetAssemblyName(path);
                
                var assembly = Assembly.Load(assemblyName);
                var stateMachineTypes = assembly.GetTypes()
                    .Where(t => typeof(IStateMachine).IsAssignableFrom(t) && 
                            t.GetTypeInfo().IsClass && 
                            !t.GetTypeInfo().IsAbstract)
                    .Select(t => t.FullName);

                foreach (var stateMachineType in stateMachineTypes)
                    context.StateMachinesToDeploy.Add(stateMachineType, assemblyFileName);
            }

            context.StateMachinesLeftToDeploy = context.StateMachinesToDeploy.Count();

            return context;
        }
    }

    public sealed class CheckForStateMachines : ChoiceState<Done>
    {
        public override List<Choice> Choices => new List<Choice>{
            new Choice<DeployStateMachine, Context>(c => c.StateMachinesLeftToDeploy > 0)
        };
    }


    public sealed class DeployStateMachine : TaskState<Context, CheckForStateMachines>
    {
        IAmazonS3 s3 = new AmazonS3Client();
        

        public override async Task<Context> Execute(Context context)
        {
            var entry = context.StateMachinesToDeploy.First();
            context.StateMachinesToDeploy.Remove(entry.Key);
            var assemblyNameOfInterest = entry.Value;
            var stateMachineFullName = entry.Key;

            var getObjectResult = await s3.GetObjectAsync(new GetObjectRequest
            {
                BucketName = context.CodeS3Bucket,
                Key = context.CodeS3Key
            });

            var assemblies = await getObjectResult.GetAssemblyNames(context);

            foreach (var assemblyFileName in assemblies) {
                if (assemblyFileName == assemblyNameOfInterest) {

                    var path = Path.Combine(Directory.GetCurrentDirectory(), assemblyFileName);
                    var assemblyName = System.Runtime.Loader.AssemblyLoadContext.GetAssemblyName(path);

                    var assembly = Assembly.Load(assemblyName);

                    
                        var stateMachineType = assembly.GetTypes()
                                .Where(t => t.FullName == stateMachineFullName)
                                .Single();
              
                    var template = DotStepBuilder.BuildCloudFormationTemplate(stateMachineType);
                   

                }
            }


            context.StateMachinesLeftToDeploy--;
            return context;
        }
    }
    
    public sealed class Done : PassState
    {
        public override bool End => true;
    }

    public static class MethodExtensions
    {
        public static async Task<IEnumerable<string>> GetAssemblyNames(this GetObjectResponse getObjectResponse, Context context)
        {

            var codeDirectory = Path.GetDirectoryName(Assembly.GetEntryAssembly().CodeBase);
            codeDirectory = codeDirectory.Replace("file:\\", string.Empty);
            var extractDirectory = codeDirectory + "\\extract";
            

            var zipFile = $"{context.CodeS3Key}.zip";

            if (File.Exists(zipFile))
                File.Delete(zipFile);
            
            await getObjectResponse.WriteResponseStreamToFileAsync(zipFile, false, CancellationToken.None);

            if (Directory.Exists(extractDirectory))
                Directory.Delete(extractDirectory, true);

            using (var zip = ZipFile.OpenRead(zipFile))
                zip.ExtractToDirectory(extractDirectory);

            foreach (var file in Directory.EnumerateFiles(extractDirectory))
            {
                var destination = file.Replace("\\extract", string.Empty);
                if (file.EndsWith(".dll") && !File.Exists(destination))
                    File.Copy(file, destination);
            }

            return Directory.EnumerateFiles(extractDirectory).Where(f => f.EndsWith(".dll"));
        }
    }
}
