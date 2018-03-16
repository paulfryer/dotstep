using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using DotStep.Core;

namespace DotStep.Common.StateMachines
{


    public sealed class CFQuickStartStateMachine: StateMachine<CFQuickStartStateMachine.Initialize>
    {

        public class Context : IContext
        {
            public string ProjectName { get; set; }
            public string ProjectZipLocation { get; set; }
            public string SourceCodeDirectory { get; set; }

            public List<string> RegionsToProcess { get; set; }
            public int RegionsLeftToProcess { get; set; }
            public Dictionary<string, string> RegionalLinks { get; set; }
        }

        public sealed class Initialize : TaskState<Context, ForEachRegion>
        {
            public override async Task<Context> Execute(Context context)
            {
                // as of November 2017, these are the only regions that support step functions.
                context.RegionsToProcess = new List<string>{
                "us-east-1",
                "us-east-2",
                "us-west-2",
                "eu-west-1",
                "eu-central-1",
                "eu-west-2",
                "ap-southeast-2",
                "ap-northeast-1"
            };

                context.RegionalLinks = new Dictionary<string, string>();

                context.RegionsLeftToProcess = context.RegionsToProcess.Count;
                return context;
            }
        }

        public sealed class ForEachRegion : ChoiceState<SaveMarkdown>
        {
            public override List<Choice> Choices
            {
                get
                {
                    return new List<Choice>{
                        new Choice<ProcessRegion, Context>(c => c.RegionsLeftToProcess > 0)
                    };
                }
            }
        }


        [Core.Action(ActionName = "s3:*")]
        public sealed class ProcessRegion : TaskState<Context, ForEachRegion>
        {
            public override async Task<Context> Execute(Context context)
            {
                var regionCode = context.RegionsToProcess.First();
                var region = RegionEndpoint.GetBySystemName(regionCode);
                var bucketName = $"dotstep-{regionCode}";

                IAmazonS3 s3 = new AmazonS3Client(region);
                if (!await s3.DoesS3BucketExistAsync(bucketName))
                {
                    var createResult = await s3.PutBucketAsync(new PutBucketRequest
                    {
                        BucketName = bucketName,
                        BucketRegion = new S3Region(regionCode),
                        CannedACL = new S3CannedACL("public-read")
                    });
                }

                string json;

                using (var netClient = new WebClient())
                    json = netClient.DownloadString(new Uri("https://raw.githubusercontent.com/paulfryer/dotstep-starter/master/DotStepStarterTemplate.json"));

                var objectName = $"{context.ProjectName.ToLower()}-template.json";
                var putObjectResult = await s3.PutObjectAsync(new PutObjectRequest
                {
                    BucketName = bucketName,
                    Key = objectName,
                    ContentType = "application/json",
                    ContentBody = json,
                    CannedACL = S3CannedACL.PublicRead
                });

                var consoleLink = $"https://{regionCode}.console.aws.amazon.com/cloudformation/home?region={regionCode}#/stacks/create/review?templateURL=https://s3.amazonaws.com/{bucketName}/{objectName}&stackName={context.ProjectName}&param_SourceCodeZip={context.ProjectZipLocation}&param_SourceCodeDirectory={context.SourceCodeDirectory}";

                context.RegionalLinks.Add(regionCode, consoleLink);
                context.RegionsToProcess.RemoveAt(0);
                context.RegionsLeftToProcess--;

                return context;
            }
        }

        public sealed class SaveMarkdown : TaskState<Context, ForEachRegion>
        {
            public override async Task<Context> Execute(Context context)
            {
                var sb = new StringBuilder();

                var isFirst = true;

                foreach (var regionCode in context.RegionalLinks.Keys)
                {
                    if (!isFirst)
                        sb.Append("|");
                    sb.Append(regionCode);
                    isFirst = false;
                }

                isFirst = true;
                foreach (var regionCode in context.RegionalLinks.Keys)
                    foreach (var character in regionCode.ToCharArray())
                    {
                        if (!isFirst)
                            sb.Append("|");
                        sb.Append("-");
                        isFirst = false;
                    }


                context.RegionalLinks = new Dictionary<string, string>();

                context.RegionsLeftToProcess = context.RegionsToProcess.Count;
                return context;
            }
        }

        public sealed class Done : PassState
        {
            public override bool End => true;
        }
    }

}