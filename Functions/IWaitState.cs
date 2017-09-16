using System;

namespace DotStep.Core
{
    public interface IWaitState {
        int Seconds { get; }
        Type Next { get; }
    }


}
