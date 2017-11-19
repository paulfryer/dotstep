using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DotStep.Core;
using Amazon.SQS;
using Amazon.SQS.Model;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;
using DotStep.StateMachines.Functions;

namespace DotStep.StateMachines.ThottledDynamoWriter
{

    public class ThrottledDynamoWriter : StateMachine<ThrottledDynamoWriter.EnsureAccountAndRegionAreSet>
    {
        public class Context : IGetExecutionInfoContext, IQueueStatsContext, IMessageProcessingContext
        {
            [Required]
            public string StateMachineName { get; set; }
            public bool AtLeastOneExecutionRunning { get; set;}
            public int MessagesWaitingForProcessing { get; set; }
            public int MessagesProcessing { get; set; }
            [Required]
            public string JobQueueName { get; set; }
            [Required]
            public int JobProcessingParallelSize { get; set; }
            public int JobProcessingCapacity { get; set; }
            [Required]
            public string MessageProcessingStateMachineArn { get; set; }
            public string JobQueueUrl { get; set; }
            public string AccountId { get; set; }
            public string RegionCode { get; set; }
            public bool NoMessagesProcessingOrWaiting { get; set; }
        }

        public class EnsureAccountAndRegionAreSet : ReferencedTaskState<Context, Initialize, EnsureAccountAndRegionAreSet<Context>> { }

        public class Initialize : ReferencedTaskState<Context, DetermineExecutionBehavior, GetExecutionInfo<Context>> { }

        public class DetermineExecutionBehavior : ChoiceState<Done>
        {
            public override List<Choice> Choices => new List<Choice>
            {
                new Choice<GetQueueStats, Context>(c => c.AtLeastOneExecutionRunning == false)
            };
        }

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

        public class ProcessMessages : ReferencedTaskState<Context, Wait, ProcessMessages<Context>> { }

        public class Wait : WaitState<GetQueueStats>
        {
            public override int Seconds => 15;
        }

        public class Done : EndState { }

    }
}
