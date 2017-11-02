using Amazon.S3;
using Amazon.S3.Model;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DotStep.Builder
{

    class Program
    {
        static void Main(string[] args)
        {

            var s3Url = args[0];
            var entry = args[1];
           
            var stateMachineFullName = entry.Split('|')[0];
            var assemblyNameOfInterest = entry.Split('|')[1];
            
            
            var s3Parts = s3Url.Split('/');

            var bucket = s3Url.Split('/')[2];
            var key = s3Url.Replace($"s3://{bucket}/", string.Empty);

            BuildCloudFormation(bucket, key, stateMachineFullName, assemblyNameOfInterest).Wait();
        }

        public static async Task BuildCloudFormation(string bucket, string key, string stateMachineFullName,string assemblyNameOfInterest)
        {
                IAmazonS3 s3 = new AmazonS3Client();

    



            var getObjectResult = s3.GetObjectAsync(new GetObjectRequest
            {
                BucketName = bucket,
                Key = key
            }).Result;

            var assemblies = await getObjectResult.GetAssemblyNames(key);

            Type stateMachineType = null;
            
            var x = Assembly.Load(System.Runtime.Loader.AssemblyLoadContext.GetAssemblyName(assemblyNameOfInterest));

            foreach (var assemblyFileName in assemblies)
            {
       
                if (assemblyFileName == assemblyNameOfInterest)
                {
                    Console.WriteLine($"Working with assembly: {assemblyFileName}");

                    var path = Path.Combine(Directory.GetCurrentDirectory(), assemblyFileName);
                    var assemblyName = System.Runtime.Loader.AssemblyLoadContext.GetAssemblyName(path);

                    var assembly = Assembly.Load(assemblyName);

                    Console.WriteLine("Searching for statemachine...");
                    stateMachineType = assembly.GetTypes()
                            .Where(t => t.FullName == stateMachineFullName)
                            .Single();
                }
            }

            var template = DotStepBuilder.BuildCloudFormationTemplate(stateMachineType);
            File.WriteAllText("template.json", template);
            
            var config = JsonConvert.SerializeObject(new
            {
                S3CodeBucket = bucket,
                S3CodeKey = key
            });

            File.WriteAllText("config.json", config);
        }
    }
}
