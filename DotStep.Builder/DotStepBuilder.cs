using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Amazon.IdentityManagement;
using Amazon.IdentityManagement.Model;
using Amazon.S3;
using Amazon.S3.Model;
using DotStep.Core;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Newtonsoft.Json;
using Action = DotStep.Core.Action;

namespace DotStep.Builder
{
    public static class DotStepBuilder
    {
        public const int DefaultTimeout = 30;
        public const int DefaultMemory = 128;

        public static void BuildTemplate(string publishLocation)
        {
            var publishDirectory = new DirectoryInfo(publishLocation);
            var assemblyFiles = publishDirectory.EnumerateFiles().Where(file => file.Extension.ToLower() == ".dll");
            var types = new List<Type>();
            foreach (var assemblyFile in assemblyFiles)
            {
                var assembly = Assembly.LoadFile(assemblyFile.FullName);
                types.AddRange(
                    assembly.ExportedTypes.Where(t => t.IsClass && typeof(IStateMachine).IsAssignableFrom(t)));
            }

            var template = BuildCloudFormationTemplates(types);
            File.WriteAllText("template.json", template);

            var version = Environment.GetEnvironmentVariable("CODEBUILD_SOURCE_VERSION");
            Console.WriteLine("Version: " + version);
            var bucket = version.Split(':')[5].Split('/')[0];
            var key = version.Split('/')[1] + "/template.json";
            var codeBuildKmsKeyId = Environment.GetEnvironmentVariable("CODEBUILD_KMS_KEY_ID");
            Console.WriteLine("Bucket: " + bucket);
            Console.WriteLine("Key: " + key);

            IAmazonS3 s3 = new AmazonS3Client();
            s3.PutObjectAsync(new PutObjectRequest
            {
                BucketName = bucket,
                Key = key,
                FilePath = "template.json",
                ContentType = "application/json",
                ServerSideEncryptionMethod = ServerSideEncryptionMethod.AWSKMS,
                ServerSideEncryptionKeyManagementServiceKeyId = codeBuildKmsKeyId
            }).Wait();
        }

        public static void BuildCloudFormationConfiguration(string s3Bucket, string s3Key)
        {
            var sb = new StringBuilder();
            sb.Append("{ \"Parameters\" : {");
            sb.Append($"\"CodeS3Bucket\" : \"{s3Bucket}\"");
            sb.Append($"\"CodeS3Key\" : \"{s3Key}\"");
            sb.Append("}}");

            var json = sb.ToString();

            var configObj = JsonConvert.DeserializeObject(json);

            var formattedJson = JsonConvert.SerializeObject(configObj, Formatting.Indented);

            File.WriteAllText("cf-config.json", formattedJson);
        }

        public static string BuildCloudFormationTemplates(List<Type> stateMachineTypes)
        {
            var template = new
            {
                Parameters = new
                {
                    CodeS3Bucket = new
                    {
                        Type = "String"
                    },
                    CodeS3Key = new
                    {
                        Type = "String"
                    }
                },
                Resources = new Dictionary<string, object>()
            };

            foreach (var stateMachineType in stateMachineTypes)
            {
                if (!typeof(IStateMachine).IsAssignableFrom(stateMachineType))
                    throw new Exception($"Type: {stateMachineType} doesn't implement {typeof(IStateMachine).Name}");

                var resources = BuildCloudFormationTemplateResources(stateMachineType);
                foreach (var resource in resources)
                    template.Resources.Add(resource.Key, resource.Value);
            }


            return JsonConvert.SerializeObject(template, Formatting.Indented);
        }

        private static Dictionary<string, object> BuildCloudFormationTemplateResources(Type stateMachineType,
            bool addActionsFromReflection = false)
        {
            var stateMachine = Activator.CreateInstance(stateMachineType) as IStateMachine;
            var stateMachineName = stateMachine.GetType().Name;

            var resources = new Dictionary<string, object>();


            var lambdaNames = new List<String>();

            //var taskStates = stateMachine.States.Where(s => s is ITaskState).ToList();

            var taskStateTypes = stateMachine.StateTypes.Where(t => typeof(ITaskState).IsAssignableFrom(t));


            foreach (var stateType in taskStateTypes)
            {
                var lambdaName = $"{stateMachineName}-{stateType.Name}";
                lambdaNames.Add(lambdaName);
                var assemblyName = stateMachine.GetType().GetTypeInfo().Assembly.GetName().Name;
                var namespaceName = stateMachine.GetType().GetTypeInfo().Namespace;

                var handler = $"{assemblyName}::{namespaceName}.{stateMachineType.Name}+{stateType.Name}::Execute";

                var memory = stateType.GetAttributeValue((FunctionMemory a) => a.Memory, DefaultMemory);
                var timeout = stateType.GetAttributeValue((FunctionTimeout a) => a.Timeout, DefaultTimeout);

                var actions = new List<string>();

                foreach (var customAction in stateType.GetTypeInfo().GetCustomAttributes<Action>())
                {
                    actions.Add(customAction.ActionName);
                }

                if (addActionsFromReflection)
                {
                    var assembly = Assembly.Load(new AssemblyName(assemblyName));
                    var assemblyDefinition = AssemblyDefinition.ReadAssembly(assembly.Location);
                    var type = assemblyDefinition.MainModule.Types.FirstOrDefault(t => t.Name == stateType.Name);
                    var executeMethod = type.Methods.First(x => x.Name == "Execute");
                    var calls = executeMethod.Body
                        .Instructions.Where(x => x.OpCode == OpCodes.Call)
                        .Select(x => x.Operand);

                    foreach (var call in calls)
                    {
                        if (call.GetType().GetProperty("GenericArguments") != null)
                        {
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
                }

                var lambdaRoleName = $"{lambdaName}-Role";
                var lambdaRole = new
                {
                    Type = "AWS::IAM::Role",
                    Properties = new
                    {
                        RoleName = lambdaRoleName,
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
                        ManagedPolicyArns = new List<string>
                        {
                            "arn:aws:iam::aws:policy/service-role/AWSLambdaBasicExecutionRole"
                        },
                        Policies = new List<dynamic>()
                    }
                };

                if (actions.Any())
                {
                    lambdaRole.Properties.Policies.Add(
                        new
                        {
                            PolicyName = $"{lambdaName}-Policy",
                            PolicyDocument = new
                            {
                                Version = "2012-10-17",
                                Statement = new
                                {
                                    Effect = actions.Any() ? "Allow" : "Deny",
                                    Resource = "*",
                                    Action = actions.Any() ? actions : new List<string> {"*"}
                                }
                            }
                        }
                    );
                }

                resources.Add(lambdaRoleName.Replace("-", string.Empty), lambdaRole);

                var functionResource = new
                {
                    Type = "AWS::Lambda::Function",
                    Properties = new
                    {
                        FunctionName = lambdaName,
                        Runtime = "dotnetcore2.1",
                        Handler = handler,
                        Timeout = timeout,
                        MemorySize = memory,
                        Code = new
                        {
                            S3Bucket = new {Ref = "CodeS3Bucket"},
                            S3Key = new {Ref = "CodeS3Key"}
                        },
                        Role = new Dictionary<string, List<string>>
                        {
                            {
                                "Fn::GetAtt", new List<string>
                                {
                                    lambdaRoleName.Replace("-", string.Empty),
                                    "Arn"
                                }
                            }
                        }
                    }
                };

                resources.Add(lambdaName.Replace("-", string.Empty), functionResource);
            }


            var stateMachineRoleName = $"{stateMachineName}-Role";
            var stateMachineRole = new
            {
                Type = "AWS::IAM::Role",
                Properties = new
                {
                    RoleName = stateMachineRoleName,
                    AssumeRolePolicyDocument = new
                    {
                        Version = "2012-10-17",
                        Statement = new
                        {
                            Effect = "Allow",
                            Principal = new
                            {
                                Service = new Dictionary<string, string>
                                {
                                    {"Fn::Sub", "states.${AWS::Region}.amazonaws.com"}
                                }
                            },
                            Action = "sts:AssumeRole"
                        }
                    },
                    Policies = new List<dynamic>
                    {
                        new
                        {
                            PolicyName = $"{stateMachineName}-Policy",
                            PolicyDocument = new
                            {
                                Version = "2012-10-17",
                                Statement = new List<dynamic>
                                {
                                    new
                                    {
                                        Effect = "Allow",
                                        Resource = lambdaNames.Select(lambdaName =>
                                            new Dictionary<string, List<string>>
                                            {
                                                {
                                                    "Fn::GetAtt", new List<string>
                                                    {
                                                        lambdaName.Replace("-", string.Empty),
                                                        "Arn"
                                                    }
                                                }
                                            }),
                                        Action = "lambda:InvokeFunction"
                                    }
                                }
                            }
                        }
                    }
                }
            };
            resources.Add(stateMachineRoleName.Replace("-", string.Empty), stateMachineRole);

            var definition = stateMachine.Describe("${AWS::Region}", "${AWS::AccountId}");

            var stateMachineResource = new
            {
                Type = "AWS::StepFunctions::StateMachine",
                Properties = new
                {
                    DefinitionString = new Dictionary<string, string> {{"Fn::Sub", definition}},
                    RoleArn = new Dictionary<string, List<string>>
                    {
                        {
                            "Fn::GetAtt", new List<string>
                            {
                                stateMachineRoleName.Replace("-", string.Empty),
                                "Arn"
                            }
                        }
                    }
                }
            };

            resources.Add(stateMachineName, stateMachineResource);

            return resources;
        }

        /*
        public static async Task BuildStateMachine<TStateMachine>(string codeBuildLocation, string releaseDirectory = "bin//release") where TStateMachine : IStateMachine
        {
            IAmazonS3 s3 = new AmazonS3Client();

            if (!Directory.Exists(releaseDirectory))
                Directory.CreateDirectory(releaseDirectory);
            var zipFileLocation = Path.Combine(releaseDirectory, $"{typeof(TStateMachine).Name}.zip");
            if (File.Exists(zipFileLocation))
                File.Delete(zipFileLocation);
            ZipFile.CreateFromDirectory(codeBuildLocation, zipFileLocation);


            var template = DotStepBuilder.BuildCloudFormationTemplate<TStateMachine>();
            File.WriteAllText($"{releaseDirectory}//template.json", template);

            var accountId = GetAccountId();
            var region = GetRegion();

            var s3Bucket = $"dotstep-builder-{region}-{accountId}";
            var s3Key = $"{GetBuildId()}/{typeof(TStateMachine).Name}.zip";

            if (!s3.DoesS3BucketExistAsync(s3Bucket).Result)
                await s3.EnsureBucketExistsAsync(s3Bucket);

            var putObjectResult = await s3.PutObjectAsync(new PutObjectRequest
            {
                BucketName = s3Bucket,
                Key = s3Key,
                ContentType = "application/zip",
                ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256,
                FilePath = zipFileLocation
            });

            var config = JsonConvert.SerializeObject(new
            {
                Parameters = new {
                    S3CodeBucket = s3Bucket,
                    S3CodeKey = s3Key
                } 
            });

            File.WriteAllText($"{releaseDirectory}//config.json", config);
        }
        */
        private static string GetAccountId()
        {
            var accountId = GetCodeBuildArn().Split(':')[4];

            if (string.IsNullOrEmpty(accountId))
            {
                IAmazonIdentityManagementService iam = new AmazonIdentityManagementServiceClient();
                var getUserResult = iam.GetUserAsync(new GetUserRequest()).Result;
                accountId = getUserResult.User.Arn.Split(':')[4];
            }

            return accountId;
        }

        private static string GetBuildId()
        {
            return GetCodeBuildArn().Split('/')[1];
        }

        private static string GetRegion()
        {
            return Environment.GetEnvironmentVariable("AWS_DEFAULT_REGION") ??
                   GetCodeBuildArn().Split(':')[3];
        }

        private static string GetCodeBuildArn()
        {
            const string defaultCodeBuildArn =
                "arn:aws:codebuild:us-west-2:account-ID:build/codebuild-demo-project:b1e6661e-e4f2-4156-9ab9-82a19EXAMPLE";
            return Environment.GetEnvironmentVariable("CODEBUILD_BUILD_ARN") ??
                   defaultCodeBuildArn;
        }
    }
}


public class DotStepUtil
{
    public static Dictionary<string, string> AmazonServiceNameToIAMNamespaceMappings =
        new Dictionary<string, string>
        {
            {"APIGateway", "apigateway"},
            {"AppStream", "appstream"},
            {"Artifact", "artifact"},
            {"AutoScaling", "autoscaling"},
            {"BillingandCostManagement", "aws-portal"},
            {"CertificateManager", "acm"},
            {"CloudDirectory", "clouddirectory"},
            {"CloudFormation", "cloudformation"},
            {"CloudFront", "cloudfront"},
            {"CloudHSM", "cloudhsm"},
            {"CloudSearch", "cloudsearch"},
            {"CloudTrail", "cloudtrail"},
            {"CloudWatch", "cloudwatch"},
            {"CloudWatchEvents", "events"},
            {"CloudWatchLogs", "logs"},
            {"CodeBuild", "codebuild"},
            {"CodeCommit", "codecommit"},
            {"CodeDeploy", "codedeploy"},
            {"CodePipeline", "codepipeline"},
            {"CodeStar", "codestar"},
            {"CognitoYourUserPools", "cognito-idp"},
            {"CognitoFederatedIdentities", "cognito-identity"},
            {"CognitoSync", "cognito-sync"},
            {"Config", "config"},
            {"DataPipeline", "datapipeline"},
            {"DatabaseMigrationService", "dms"},
            {"DeviceFarm", "devicefarm"},
            {"DirectConnect", "directconnect"},
            {"DirectoryService", "ds"},
            {"DynamoDB", "dynamodb"},
            {"ElasticComputeCloud", "ec2"},
            {"EC2ContainerRegistry", "ecr"},
            {"EC2ContainerService", "ecs"},
            {"EC2SystemsManager", "ssm"},
            {"ElasticBeanstalk", "elasticbeanstalk"},
            {"ElasticFileSystem", "elasticfilesystem"},
            {"ElasticLoadBalancing", "elasticloadbalancing"},
            {"EMR", "elasticmapreduce"},
            {"ElasticTranscoder", "elastictranscoder"},
            {"ElastiCache", "elasticache"},
            {"ElasticsearchService(ES)", "es"},
            {"GameLift", "gamelift"},
            {"Glacier", "glacier"},
            {"Glue", "glue"},
            {"Health/PersonalHealthDashboard", "health"},
            {"IdentityManagement", "iam"},
            {"Import/Export", "importexport"},
            {"Inspector", "inspector"},
            {"IoT", "iot"},
            {"KeyManagementService", "kms"},
            {"KinesisAnalytics", "kinesisanalytics"},
            {"KinesisFirehose", "firehose"},
            {"KinesisStreams", "kinesis"},
            {"Lambda", "lambda"},
            {"Lightsail", "lightsail"},
            {"MachineLearning", "machinelearning"},
            {"Marketplace", "aws-marketplace"},
            {"MarketplaceManagementPortal", "aws-marketplace-management"},
            {"MobileAnalytics", "mobileanalytics"},
            {"MobileHub", "mobilehub"},
            {"OpsWorks", "opsworks"},
            {"OpsWorksforChefAutomate", "opsworks-cm"},
            {"Organizations", "organizations"},
            {"Polly", "polly"},
            {"Redshift", "redshift"},
            {"RelationalDatabaseService", "rds"},
            {"Route53", "route53"},
            {"Route53Domains", "route53domains"},
            {"SecurityToken", "sts"},
            {"ServiceCatalog", "servicecatalog"},
            {"SimpleEmailService", "ses"},
            {"SimpleNotificationService", "sns"},
            {"SQS", "sqs"},
            {"S3", "s3"},
            {"SimpleWorkflowService", "swf"},
            {"SimpleDB", "sdb"},
            {"StepFunctions", "states"},
            {"StorageGateway", "storagegateway"},
            {"Support", "support"},
            {"TrustedAdvisor", "trustedadvisor"},
            {"VirtualPrivateCloud", "ec2"},
            {"WAF", "waf"},
            {"WorkDocs", "workdocs"},
            {"WorkMail", "workmail"},
            {"WorkSpaces", "workspaces"}
        };

    public static string GetIAMNamespace(string amazonService)
    {
        var iamNamespace = AmazonServiceNameToIAMNamespaceMappings
            .Where(m => m.Key.ToLower() == amazonService.ToLower())
            .Select(m => m.Value)
            .SingleOrDefault();

        if (string.IsNullOrEmpty(iamNamespace))
            throw new Exception($"Cloud not find a IAM namespace mapping for Amazon service: {amazonService}");

        return iamNamespace;
    }
}