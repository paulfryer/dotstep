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

namespace DotStep.StateMachines.Publish
{

    public sealed class PublishStateMachine : StateMachine<InitializeContext>
    {
    }

    public class PublishContext : IContext
    {
        public string CodeS3Bucket { get; set; }
        public string CodeS3Key { get; set; }
        public string CodeStateMachineClassName { get; set; }

        public int RemainingLambdasToPublish { get; internal set; }
        public List<string> LambdasToPublish { get; internal set; }

        public bool PublishStepFunction { get; internal set; }
        public int StepFunctionVersion { get; internal set; }
        public dynamic StepFunctionDefinition { get; internal set; }

        public string AccountId { get; internal set; }
        public string Region { get; set; }
    }

    public sealed class InitializeContext : TaskState<PublishContext, GetLambdasToPublish>
    {
        IAmazonIdentityManagementService iam = new AmazonIdentityManagementServiceClient();

        public override async Task<PublishContext> Execute(PublishContext context)
        {
            if (String.IsNullOrEmpty(context.Region))
                context.Region = Amazon.Util.EC2InstanceMetadata.Region.SystemName;
            var getUserResult = await iam.GetUserAsync(new GetUserRequest { });
            context.AccountId = getUserResult.User.Arn.Split(':')[4];
            context.LambdasToPublish = new List<string>();
            return context;
        }
    }



    public sealed class GetLambdasToPublish : TaskState<PublishContext, ForEachLambda>
    {
        public override async Task<PublishContext> Execute(PublishContext context)
        {            
            foreach (var taskType in await context.GetTaskStateTypes())                    
                context.LambdasToPublish.Add(taskType.FullName);
            context.RemainingLambdasToPublish = context.LambdasToPublish.Count;
            return context;
        }
    }

    public sealed class ForEachLambda : ChoiceState<GetStateMachineContext>
    {
        public override List<Choice> Choices => new List<Choice>{
            new Choice<PublishLambda, PublishContext>(c => c.RemainingLambdasToPublish > 0)
        };
    }

    public sealed class PublishLambda : TaskState<PublishContext, ForEachLambda>
    {
        public const string DefaultLambdaRoleName = "lambda_basic_execution";
        public const int DefaultTimeout = 30;
        public const int DefaultMemory = 128;

        public override async Task<PublishContext> Execute(PublishContext context)
        {
            var lambdaTypeFullName = context.LambdasToPublish.First();
            context.LambdasToPublish.RemoveAt(0);

            Console.WriteLine($"Publishing lambda: {lambdaTypeFullName}");


            context.RemainingLambdasToPublish--;
            return context;
        }
    }

    public sealed class GetStateMachineContext : TaskState<PublishContext, DetermineStepFunctionPublishingBehavior>
    {
        IAmazonStepFunctions stepFunctions = new AmazonStepFunctionsClient();

        public override async Task<PublishContext> Execute(PublishContext context)
        {
            var stateMachine = Activator.CreateInstance(await context.GetStateMachineType()) as IStateMachine;

            var definition = stateMachine.Describe(context.Region, context.AccountId);
            context.StepFunctionVersion = 1;
            context.PublishStepFunction = true;

            var listResult = await stepFunctions.ListStateMachinesAsync(new ListStateMachinesRequest
            {
                MaxResults = 1000
            });
            var latestVersion = listResult.StateMachines
                                .Where(sm => sm.StateMachineArn.Contains(context.CodeStateMachineClassName))
                                .Select(sm => sm.StateMachineArn)
                                .OrderByDescending(arn => arn)
                                .FirstOrDefault();

            if (latestVersion != null)
            {
                Console.WriteLine("Existsing version found: " + latestVersion);
                context.StepFunctionVersion = Convert.ToInt16(latestVersion.Substring(latestVersion.Length - 3));
                var descriptionResult = await stepFunctions.DescribeStateMachineAsync(new DescribeStateMachineRequest
                {
                    StateMachineArn = latestVersion
                });

                if (descriptionResult.Definition != definition)
                {
                    Console.WriteLine("Step function definintion has changed, creating new version.");                    
                    context.StepFunctionVersion++;
                }
                else
                    context.PublishStepFunction = false;
            }

            if (context.PublishStepFunction)
                context.StepFunctionDefinition = JsonConvert.DeserializeObject<dynamic>(definition);

            return context;
        }
    }

    public sealed class DetermineStepFunctionPublishingBehavior : ChoiceState<Done>
    {
        public override List<Choice> Choices => new List<Choice> {
            new Choice<PublishStepFunction, PublishContext>(c => c.PublishStepFunction == true)
        };
    }

    public sealed class PublishStepFunction : TaskState<PublishContext, Done>
    {
        public const string DefaultStatesExecutionRoleName = "StatesExecutionRole-{region}";

        IAmazonStepFunctions stepFunctions = new AmazonStepFunctionsClient();

        public override async Task<PublishContext> Execute(PublishContext context)
        {
            var paddedVersion = context.StepFunctionVersion.ToString().PadLeft(3, '0');
            var stateMachineName = $"{context.CodeStateMachineClassName}-v{ paddedVersion}";
            var definition = JsonConvert.SerializeObject(context.StepFunctionDefinition, Formatting.Indented);
            var stateMachineRoleName = this.GetAttributeValue((ExplicitRole a) => a.RoleName, DefaultStatesExecutionRoleName.Replace("{region}", context.Region));
            var stateMachineRoleArn = $"arn:aws:iam::{context.AccountId}:role/service-role/{stateMachineRoleName}";

            var createStateMachineResult = await stepFunctions.CreateStateMachineAsync(new CreateStateMachineRequest
            {
                Definition = definition,
                Name = stateMachineName,
                RoleArn = stateMachineRoleArn
            });
            Console.Write(createStateMachineResult.StateMachineArn);
            return context;
        }
    }

    public sealed class Done : PassState
    {
        public override bool End => true;
    }

    public static class PublishMethodExtensions
    {
        public static async Task<List<Type>> GetTaskStateTypes(this PublishContext context)
        {
            var taskStates = new List<ITaskState>();
            foreach (var assemblyFileName in await context.GetAssemblyNames())
            {
                var path = Path.Combine(Directory.GetCurrentDirectory(), assemblyFileName);
                var assemblyName = System.Runtime.Loader.AssemblyLoadContext.GetAssemblyName(path);
                var assembly = Assembly.Load(assemblyName);
                var stateMachineType = assembly.GetTypes().SingleOrDefault(t => t.Name == context.CodeStateMachineClassName);
                if (stateMachineType != null)
                    return assembly.GetTypes()
                        .Where(t => typeof(ITaskState).IsAssignableFrom(t) && t.Namespace == stateMachineType.Namespace)
                        .ToList();   
            }
            return new List<Type>();
        }

        public static async Task<Type> GetStateMachineType(this PublishContext context) {

            foreach (var assemblyFileName in await context.GetAssemblyNames())
            {
                var path = Path.Combine(Directory.GetCurrentDirectory(), assemblyFileName);
                var assemblyName = System.Runtime.Loader.AssemblyLoadContext.GetAssemblyName(path);
                var assembly = Assembly.Load(assemblyName);
                var stateMachineType = assembly.GetTypes().SingleOrDefault(t => t.Name == context.CodeStateMachineClassName);
                if (stateMachineType != null)                
                    return stateMachineType;                
            }
            throw new Exception($"Cloud not find state machine type: {context.CodeStateMachineClassName}");
        }

        public static async Task<IEnumerable<string>> GetAssemblyNames(this PublishContext context)
        {

            IAmazonS3 s3 = new AmazonS3Client();

            var getObjectResult = await s3.GetObjectAsync(new GetObjectRequest
            {
                BucketName = context.CodeS3Bucket,
                Key = context.CodeS3Key
            });

            var zipFile = $"{context.CodeS3Key}.zip";

            if (File.Exists(zipFile))
                File.Delete(zipFile);

            await getObjectResult.WriteResponseStreamToFileAsync(zipFile, false, CancellationToken.None);

            var zip = ZipFile.OpenRead(zipFile);

            if (Directory.Exists("extract"))
                Directory.Delete("extract", true);
            Directory.CreateDirectory("extract");

            zip.ExtractToDirectory("extract");

            zip.Dispose();

            return Directory.EnumerateFiles("extract").Where(f => f.EndsWith(".dll"));
        }
    }
}
