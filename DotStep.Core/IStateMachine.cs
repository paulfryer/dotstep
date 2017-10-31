using Amazon.Runtime;
using System;
using System.Threading.Tasks;

namespace DotStep.Core
{
    public interface IStateMachine
    {
        Type StartAt { get; }
        string Describe(string region, string accountId);
        Task PublishAsync(AWSCredentials awsCredentials, string region, string accountId);

        Task<string> BuildCloudFormationTemplate();
    }

}
