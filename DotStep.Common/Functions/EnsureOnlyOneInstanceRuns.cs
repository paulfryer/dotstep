using System.Linq;
using System.Threading.Tasks;
using Amazon.StepFunctions;
using Amazon.StepFunctions.Model;
using DotStep.Core;

namespace DotStep.Common.Functions
{
    public interface IGetExecutionInfoContext : IAccountContext, IRegionContext
    {
        string StateMachineName { get; set; }
        int RunningExecutionsCount { get; set; }
    }


    public sealed class GetExecutionInfo<TContext> : LambdaFunction<TContext>
        where TContext : IGetExecutionInfoContext
    {
        readonly IAmazonStepFunctions stepFunctions = new AmazonStepFunctionsClient();

        public override async Task<TContext> Execute(TContext context)
        {
            var stateMachineArn =
                $"arn:aws:states:{context.RegionCode}:{context.AccountId}:stateMachine:{context.StateMachineName}";

            var historyResult = await stepFunctions.ListExecutionsAsync(new ListExecutionsRequest
            {
                StateMachineArn = stateMachineArn
            });

            context.RunningExecutionsCount = historyResult.Executions.Count(e => e.Status.Value == "RUNNING");

            return context;
        }
    }
}