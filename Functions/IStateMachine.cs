using System;

namespace DotStep.Core
{
    public interface IStateMachine {
        Type StartAt { get; }
        string Describe(string region, string accountId);
        string Publish(string region, string accountId);
    }


}
