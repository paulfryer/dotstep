using System.Collections.Generic;
using DotStep.Core;
using DotStep.Common.Functions;

namespace DotStep.Common.StateMachines
{
    public class ThrottledProcessor : StateMachine<ThrottledProcessor.EnsureAccountAndRegionAreSet>
    {
        public class Context : IGetExecutionInfoContext, IQueueStatsContext, IMessageProcessingContext
        {
            [Required]
            public string StateMachineName { get; set; }
            public int RunningExecutionsCount { get; set;}
            public int MessagesWaitingForProcessing { get; set; }
            public int MessagesProcessing { get; set; }
            [Required]
            public string JobQueueName { get; set; }
            [Required]
            public int JobProcessingParallelSize { get; set; }
            public int JobProcessingCapacity { get; set; }
            public string JobQueueUrl { get; set; }
            public string AccountId { get; set; }
            public string RegionCode { get; set; }
            public bool NoMessagesProcessingOrWaiting { get; set; }
            [Required]
            public string MessageProcessorType { get; set; }
            [Required]
            public string MessageProcessorName { get; set; }
        }

        [Action(ActionName = "sts:GetCallerIdentity")]
        public class EnsureAccountAndRegionAreSet : ReferencedTaskState<Context, Initialize, EnsureAccountAndRegionAreSet<Context>> { }

        [Action(ActionName = "states:ListExecutions")]
        public class Initialize : ReferencedTaskState<Context, DetermineExecutionBehavior, GetExecutionInfo<Context>> { }

        public class DetermineExecutionBehavior : ChoiceState<Done>
        {
            public override List<Choice> Choices => new List<Choice>
            {
                new Choice<GetQueueStats, Context>(c => c.RunningExecutionsCount <= 1)
            };
        }

        [Action(ActionName = "sqs:GetQueueAttributes")]
        public class GetQueueStats : ReferencedTaskState<Context, DetermineProcessingBehavior, GetQueueStats<Context>> { }
        
        public class DetermineProcessingBehavior : ChoiceState<Done>
        {
            public override List<Choice> Choices => new List<Choice>
            {
                new Choice<Done, Context>(c => c.NoMessagesProcessingOrWaiting == true),
                new Choice<Wait, Context>(c => c.JobProcessingCapacity <= 0),
                new Choice<ProcessMessages, Context>(c => c.MessagesWaitingForProcessing > 0)             
            };
        }

        [Action(ActionName = "sqs:DeleteMessage")]
        [Action(ActionName = "sqs:ReceiveMessage")]
        [Action(ActionName = "states:StartExecution")]
        public class ProcessMessages : ReferencedTaskState<Context, Wait, ProcessMessages<Context>> { }

        public class Wait : WaitState<GetQueueStats>
        {
            public override int Seconds => 15;
        }

        public class Done : EndState { }
    }
}
