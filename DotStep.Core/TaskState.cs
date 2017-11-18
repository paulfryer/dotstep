using System;
using System.Threading.Tasks;
using System.Reflection;
using Amazon.Lambda.Core;

namespace DotStep.Core
{
    public abstract class TaskState<TContext, TNext> : State, ITaskState<TContext> 
        where TNext : IState
        where TContext : IContext
    {
        public Type Next => GetType().GetTypeInfo().BaseType.GenericTypeArguments[1];

        [LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]
        public abstract Task<TContext> Execute(TContext context);
    }

    public abstract class ReferencedTaskState<TContext, TNext, TLambda> : TaskState<TContext, TNext>
        where TContext : IContext
        where TNext : IState
        where TLambda : ILambdaFunction
    {
        public override Task<TContext> Execute(TContext context)
        {
            var lambda = Activator.CreateInstance(typeof(TLambda)) as ILambdaFunction<TContext>;
            return lambda.Execute(context);
        }
    }

    public interface ILambdaFunction { }

    public interface ILambdaFunction<TContext> : ILambdaFunction 
        where TContext : IContext
    {
        Task<TContext> Execute(TContext context);
    }

    public abstract class LambdaFunction<TContext> : ILambdaFunction<TContext> 
        where TContext : IContext
    {
        [LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]
        public abstract Task<TContext> Execute(TContext context);
    }
}
