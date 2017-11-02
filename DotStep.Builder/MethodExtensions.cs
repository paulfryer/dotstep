using Amazon.S3.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DotStep.Builder
{
    public static class MethodExtensions
    {
        public static async Task<IEnumerable<string>> GetAssemblyNames(this GetObjectResponse getObjectResponse, string s3CodeKey)
        {
            Console.WriteLine($"Current dir: {Directory.GetCurrentDirectory()}");
            var codeDirectory = "/tmp";

            codeDirectory = codeDirectory.Replace("file:\\", string.Empty);
            var extractDirectory = codeDirectory + "/extract";
            Console.WriteLine($"extractDirectory: {extractDirectory}");

            var zipFile = $"/tmp/{s3CodeKey}.zip";

            if (File.Exists(zipFile))
                File.Delete(zipFile);

            await getObjectResponse.WriteResponseStreamToFileAsync(zipFile, false, CancellationToken.None);

            if (Directory.Exists(extractDirectory))
                Directory.Delete(extractDirectory, true);
            Console.WriteLine("About to unzip the file..");
            using (var zip = ZipFile.OpenRead(zipFile))
                zip.ExtractToDirectory(extractDirectory);


            foreach (var file in Directory.EnumerateFiles(extractDirectory))
            {
                var destination = file.Replace("/extract", string.Empty);
                destination = destination.Replace("\\", "/");
                if (file.EndsWith(".dll") && !File.Exists(destination))
                    File.Copy(file, destination);
            }

            return Directory.EnumerateFiles(extractDirectory).Where(f => f.EndsWith(".dll"));
        }
    }
}
