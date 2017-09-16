using System;
using System.Threading.Tasks;
using System.Reflection;

namespace DotStep.Core
{
    public abstract class TaskState<TContext, TNext> : State, ITaskState<TContext>
        where TContext : IContext
        where TNext : IState
    {
        public Type Next {
            get {
                return GetType().GetTypeInfo().BaseType.GenericTypeArguments[1];
            }
        }

        public abstract Task<TContext> Execute(TContext context);
    }
}
