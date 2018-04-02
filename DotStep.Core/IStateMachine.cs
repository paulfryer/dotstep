using System;
using System.Collections.Generic;

namespace DotStep.Core
{
    public interface IStateMachine
    {
        Type StartAt { get; }
        IEnumerable<IState> States { get; }
        IEnumerable<Type> StateTypes { get; }
        string Describe(string region, string accountId);
    }

}
