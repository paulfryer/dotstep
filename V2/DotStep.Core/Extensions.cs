using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Amazon.Runtime;
using DotStep.Core.States;

namespace DotStep.Core
{
    public class CustomExpressionVisitor : ExpressionVisitor
    {

        public override Expression Visit(Expression node)
        {
            Console.WriteLine($"{node.NodeType} {node.Type} {node.CanReduce} {node.ToString()}");
            return base.Visit(node);
        }

    }


    public static class Extensions
    {

        public static ChoiceSate<TInput> AddRule<TInput>(this ChoiceSate<TInput> state,
            Expression<Func<TInput, bool>> conditions, IState nextState)
        {

            var RuleMapping = conditions.Compile();

            var visitor = new CustomExpressionVisitor();
            visitor.Visit(conditions);




            return state;
        }
        public static MapState<TInput, TIterator, TRequest> SetMapping<TInput, TIterator, TRequest>(
            this MapState<TInput, TIterator, TRequest> state,
            Expression<Func<TIterator, TRequest>> mapping)
        {
            state.Mapping = mapping.Compile();
            return state;
        }

        public static MapState<TInput, TIterator, TRequest> SetIterator<TInput, TIterator, TRequest>(
            this MapState<TInput, TIterator, TRequest> state,
            Expression<Func<TInput, List<TIterator>>> iterator)
        {
            state.Iterator = iterator.Compile();
            return state;
        }


        public static AmazonStateTask<TInput, TClient, TRequest, TResponse> SetName<TInput, TClient, TRequest,
            TResponse>(
            this AmazonStateTask<TInput, TClient, TRequest, TResponse> task, string name)
            where TClient : AmazonServiceClient
            where TResponse : AmazonWebServiceResponse
            where TRequest : AmazonWebServiceRequest
        {
            task.Name = name;
            return task;
        }

        public static AmazonStateTask<TInput, TClient, TRequest, TResponse> SetParameters<TInput, TClient, TRequest,
            TResponse>(
            this AmazonStateTask<TInput, TClient, TRequest, TResponse> task,
            Expression<Func<TInput, TRequest>> mapping)
            where TClient : AmazonServiceClient
            where TResponse : AmazonWebServiceResponse
            where TRequest : AmazonWebServiceRequest
        {
            task.Mapping = mapping.Compile();

            var parameter = mapping.Parameters.Single();


            var nodeType = mapping.Body.NodeType;

            switch (nodeType)
            {
                case ExpressionType.Parameter:
                    var parameterExpression = (ParameterExpression)mapping.Body;

                    break;
                case ExpressionType.MemberInit:
                    foreach (var binding in ((MemberInitExpression)mapping.Body).Bindings)
                        task.Parameters.Add(binding.Member.Name, "x.something");
                    break;
                default: throw new NotImplementedException(Convert.ToString(nodeType));
            }


            return task;
        }

        public static TransitionalState SetNextState(
            this TransitionalState transitionalState, IState nextState)
        {
            transitionalState.NextState = nextState;
            return transitionalState;
        }

        public static ParallelState AddState(this ParallelState parallelState, IState state)
        {
            parallelState.States.Add(state);
            return parallelState;
        }


        public static AmazonStateTask<TInput, TClient, TRequest, TResponse> AddErrorHandler<TInput, TClient, TRequest,
            TResponse>(
            this AmazonStateTask<TInput, TClient, TRequest, TResponse> task,
            Type errorType,
            ErrorHandler errorHandler)
            where TClient : AmazonServiceClient
            where TResponse : AmazonWebServiceResponse
            where TRequest : AmazonWebServiceRequest
        {
            task.ErrorHandlers.Add(errorType, errorHandler);
            return task;
        }

        public static AmazonStateTask<TInput, TClient, TRequest, TResponse> 
            Catch<TInput, TClient, TRequest, TResponse>(
           this AmazonStateTask<TInput, TClient, TRequest, TResponse> task, Type exceptionType, IState fallbackState)
          //  where TException : Exception
           where TClient : AmazonServiceClient
           where TResponse : AmazonWebServiceResponse
           where TRequest : AmazonWebServiceRequest
        {

            //TODO: register the error fallback mapping.

            // WOrk in progress.
            return task;
        }
    }
}