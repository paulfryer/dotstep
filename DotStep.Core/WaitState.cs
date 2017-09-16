using System;
using System.Reflection;

namespace DotStep.Core
{
    public abstract class WaitState<TNext> : State, IWaitState where TNext : IState
    {
        public abstract int Seconds { get; }
        public Type Next { get {
                return GetType().GetTypeInfo().BaseType.GenericTypeArguments[0];
            }  }
    }
}
