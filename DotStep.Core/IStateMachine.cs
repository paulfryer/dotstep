using System;
using System.Threading.Tasks;

namespace DotStep.Core
{
    public interface IStateMachine {
        Type StartAt { get; }
        string Describe(string region, string accountId);
        Task PublishAsync(string region, string accountId);
        Task PublishAsync(string region, string accountId, string roleName, string publishLocation);
    }


}
