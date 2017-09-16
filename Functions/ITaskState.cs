using System;

namespace DotStep.Core
{
    public interface ITaskState : IState {
        Type Next { get; }
    }


}
