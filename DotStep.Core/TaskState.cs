using System;
using System.Threading.Tasks;
using System.Reflection;
using Amazon.Lambda.Core;

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

        [LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]
        public abstract Task<TContext> Execute(TContext context);
    }
}
