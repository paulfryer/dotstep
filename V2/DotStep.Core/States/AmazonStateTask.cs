using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Runtime;
using Amazon.Runtime.Internal;
using Newtonsoft.Json;

namespace DotStep.Core.States
{
    public interface IAmazonStateTask
    {
        public string Arn { get; }
        public Dictionary<string, object> Parameters { get; }

        public Dictionary<Type, ErrorHandler> ErrorHandlers { get; }
    }

    public class AmazonStateTask<TInput, TClient, TRequest, TResponse> : TransitionalState, IAmazonStateTask
        where TClient : AmazonServiceClient
        where TRequest : AmazonWebServiceRequest
        where TResponse : AmazonWebServiceResponse
    {
        private readonly MethodInfo method;

        public Func<TInput, TRequest> Mapping;

        public AmazonStateTask()
        {
            Parameters = new Dictionary<string, object>();
            Client = Activator.CreateInstance<TClient>();
            method = GetMethod(Client.GetType(), typeof(TRequest), typeof(Task<TResponse>));

            // Set the default name (TODO: think about how to avoid duplicate names)
            Name = typeof(TRequest).Name.Replace("Request", string.Empty);
        }

        public TClient Client { get; set; }

        public TRequest Request { get; set; }
        public TResponse Response { get; set; }

        public Dictionary<Type, ErrorHandler> ErrorHandlers { get; set; } =
            new AutoConstructedDictionary<Type, ErrorHandler>();

        public string Arn
        {
            get
            {
                var serviceName = Client.GetType().Name.Replace("Client", string.Empty).Replace("Amazon", string.Empty)
                    .ToLower();
                var actionName = method.Name;

                if (actionName.EndsWith("Async"))
                    actionName = actionName.Substring(0, actionName.Length - "Async".Length);

                actionName = actionName.Substring(0, 1).ToLower() +
                             actionName.Substring(1, actionName.Length - 1);

                return $"arn:aws:states:::aws-sdk:{serviceName}:{actionName}";
            }
        }


        public Dictionary<string, object> Parameters { get; set; }

        private MethodInfo GetMethod(Type clientType, Type requestType, Type responseType)
        {
            var methods = Client.GetType().GetMethods();
            return methods.Single(m =>
                m.ReturnParameter.ParameterType == responseType &&
                m.GetParameters().First().ParameterType == requestType);
        }

        public override async Task Transition()
        {
            try
            {
                // Note: this step is necessary because anonymous types from outside assemblies are not referenced
                // as objects, so their properties are not accessible.
                // See: https://stackoverflow.com/questions/9416095/dynamic-does-not-contain-a-definition-for-a-property-from-a-project-reference
                Input = JsonConvert.DeserializeObject<TInput>(JsonConvert.SerializeObject(Input));

                Request = Mapping.Invoke(Input);
                Response = await (Task<TResponse>)method.Invoke(Client,
                    new object[] { Request, CancellationToken.None });

                //  by default we set the response as the output
                Output = Response;
            }
            catch (Exception exception)
            {
                if (ErrorHandlers.ContainsKey(exception.GetType()))
                {
                    var execptionType = exception.GetType();
                    var handler = ErrorHandlers[execptionType];

                    if (handler.FallbackState is TransitionalState transitionalFallbackState)
                    {
                        // TODO: apply Result Path filter.
                        // Find the method to invoke next, then transition to it.

                        transitionalFallbackState.Input = Input;
                        await transitionalFallbackState.Transition();
                    }
                }


                throw;
            }
            // Apply response mappings


            // Set the next states intput to this ones output.
            if (NextState != null)
                NextState.Input = Output;

            Console.WriteLine($"State: {Name}");
            Console.WriteLine();


            if (NextState is TransitionalState transitionalState)
                await transitionalState.Transition();
        }
    }
}