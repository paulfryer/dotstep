using System;
using System.Collections.Generic;

namespace DotStep.Core
{
    public abstract class ParallelState<TContext> : State, IParallelState<TContext>
    {
        public abstract List<Type> ParallelStateTypes { get; }

        public abstract Type Next { get; }
    }
}
