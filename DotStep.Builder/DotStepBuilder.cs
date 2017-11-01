using Mono.Cecil;
using Mono.Cecil.Cil;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace DotStep.Core
{

    public static class DotStepBuilder
    {
        public const int DefaultTimeout = 30;
        public const int DefaultMemory = 128;
        
        public static string BuildCloudFormationTemplate<TStateMachine>() where TStateMachine : IStateMachine
        {
            var stateMachineType = typeof(TStateMachine);
            return BuildCloudFormationTemplate(stateMachineType);
        }

        public static string BuildCloudFormationTemplate(Type stateMachineType)
        {
            var stateMachine = Activator.CreateInstance(stateMachineType) as IStateMachine;
            var stateMachineName = stateMachine.GetType().Name;

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

            foreach (var state in stateMachine.States.Where(s => s is ITaskState))
            {
                var lambdaName = $"{stateMachineName}-{state.Name}";
                lambdaNames.Add(lambdaName);
                var assemblyName = stateMachine.GetType().GetTypeInfo().Assembly.GetName().Name;
                var namespaceName = stateMachine.GetType().GetTypeInfo().Namespace;
                
                var handler = $"{assemblyName}::{namespaceName}.{state.Name}::Execute";
                
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
                        ManagedPolicyArns = new List<string> {
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
                                        Action = actions.Any() ? actions : new List<string> { "*" }
                                    }
                                }
                            }
                        );
                }

                template.Resources.Add(lambdaRoleName.Replace("-", string.Empty), lambdaRole);
     
                var functionResource = new
                {
                    Type = "AWS::Lambda::Function",
                    Properties = new {
                        FunctionName = lambdaName,
                        Runtime = "dotnetcore1.0",
                        Handler = handler,
                        Timeout = timeout,
                        MemorySize = memory,
                        Code = new {
                            S3Bucket = new { Ref = "CodeS3Bucket" },
                            S3Key = new {Ref="CodeS3Key"}
                        },
                        Role = new Dictionary<string, List<string>> {
                            { "Fn::GetAtt", new List<string>{
                                lambdaRoleName.Replace("-", string.Empty), "Arn"
                            } }
                        }
                    }
                };

                template.Resources.Add(lambdaName.Replace("-", string.Empty), functionResource);                

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
                                Service = new Dictionary<string, string> {
                                    { "Fn::Sub", "states.${AWS::Region}.amazonaws.com" }
                                }
                            },
                            Action = "sts:AssumeRole"
                        }
                    },
                    Policies = new List<dynamic> {
                        new {
                            PolicyName = $"{stateMachineName}-Policy",
                            PolicyDocument = new
                                {
                                    Version = "2012-10-17",
                                    Statement = new List<dynamic> { new
                                        {
                                            Effect = "Allow",
                                            Resource = lambdaNames.Select(lambdaName =>
                                                new Dictionary<string, List<string>> {
                                                    {
                                                        "Fn::GetAtt", new List<string>{
                                                                            lambdaName.Replace("-", string.Empty), "Arn"
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
            template.Resources.Add(stateMachineRoleName.Replace("-", string.Empty), stateMachineRole);

            var definition = stateMachine.Describe("${AWS::Region}", "${AWS::AccountId}");

            var stateMachineResource = new
            {
                Type = "AWS::StepFunctions::StateMachine",
                Properties = new {
                    DefinitionString = new Dictionary<string, string> { { "Fn::Sub", definition } },
                        RoleArn = new Dictionary<string, List<string>> {
                            { "Fn::GetAtt", new List<string>{
                                stateMachineRoleName.Replace("-", string.Empty), "Arn"
                            } }
                        }
                }

            };

            template.Resources.Add(stateMachineName, stateMachineResource);
            
            var json = JsonConvert.SerializeObject(template, Formatting.Indented);

            return json;
        }



        /*
        public async Task PublishAsync(
            AWSCredentials awsCredentials,
            string region, 
            string accountId,
            string publishLocation)
        {
            IAmazonLambda lambda = new AmazonLambdaClient(awsCredentials);
            IAmazonStepFunctions stepFunctions = new AmazonStepFunctionsClient(awsCredentials);

            var statMachineName = GetType().Name;

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
        */
    }


}


public class DotStepUtil
{
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
                {"SecurityTokenService", "sts"},
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
                {"WorkSpaces", "workspaces"},
    };
}