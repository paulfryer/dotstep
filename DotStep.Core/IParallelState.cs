using System;
using System.Collections.Generic;

namespace DotStep.Core
{
    public interface IParallelState<TContext> : IState {
        List<Type> ParallelStateTypes { get; } 
        Type Next { get; }
    }
}
