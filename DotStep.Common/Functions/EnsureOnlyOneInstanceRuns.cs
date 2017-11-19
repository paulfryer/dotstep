using System.Threading.Tasks;
using DotStep.Core;
using Amazon.StepFunctions;
using Amazon.StepFunctions.Model;
using System;
using System.Linq;

namespace DotStep.Common.Functions
{
    public interface IGetExecutionInfoContext : IAccountContext, IRegionContext
    {
        string StateMachineName { get; set; }
        bool AtLeastOneExecutionRunning { get; set; }
    }
    

    public sealed class GetExecutionInfo<TContext> : LambdaFunction<TContext> 
        where TContext : IGetExecutionInfoContext
    {
        IAmazonStepFunctions stepFunctions = new AmazonStepFunctionsClient();                
        public override async Task<TContext> Execute(TContext context) 
        {
            var stateMachineArn = $"arn:aws:states:{context.RegionCode}:{context.AccountId}:stateMachine:{context.StateMachineName}";

                var historyResult = await stepFunctions.ListExecutionsAsync(new ListExecutionsRequest
                {                    
                    StateMachineArn = stateMachineArn
                });

                context.AtLeastOneExecutionRunning = historyResult.Executions.Any(e => e.Status.Value == "RUNNING");

            return context;
        }
    }
}
