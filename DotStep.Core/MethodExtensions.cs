using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace DotStep.Core
{
    public static class MethodExtensions
    {
        public static T GetAttributeValue<T, TAttributeType>(this object obj, Func<TAttributeType, T> func, T defaultValue) where TAttributeType : Attribute
        {
            var attribute = obj.GetType().GetTypeInfo().GetCustomAttributes<TAttributeType>().SingleOrDefault();
            var value = attribute != null ? func(attribute) : defaultValue;
            return value;
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
                {"SimpleQueueService", "sqs"},
                {"SimpleStorageService", "s3"},
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
}
