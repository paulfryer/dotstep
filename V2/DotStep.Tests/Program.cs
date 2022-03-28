using System;
using Amazon.S3.Model;
using DotStep.Core;
using DotStep.Core.StateEngines;
using DotStep.Reference.StateMachines;

namespace DotStep.Tests
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            var request = new ListObjectsV2Request
            {
                BucketName = "somebucket"
            };

            // var s3 = (AmazonS3Client)Activator.CreateInstance<AmazonS3Client>();
            // var x = s3.ListObjectsV2Async(request, CancellationToken.None).Result;

           // var generator = new StepFunctionGenerator<IndexAllFaces>();
           // generator.GenerateStateMachine();

            var input = new
            {
                Bucket = "crowdarise-web",
                Collection = "mycolloection",
                Prefix = "index",
                TableName = "sometable"
            };


            IStateEngine stateEngine = new LocalStateEngine();

            stateEngine.Run<IndexAllFaces>(input).Wait();

            Console.WriteLine("Done running state machine.");
            Console.ReadKey();
        }
    }
}