using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DotStep.Core;
using Amazon.StepFunctions;
using Amazon.StepFunctions.Model;
using Amazon.SQS;
using Amazon.SQS.Model;

namespace DotStep.StateMachines.StepFunctionQueue
{
    public sealed class StepFunctionQueueStateMachine : StateMachine<GetQueueStats>
    {
    }
    public class SFQueueContext : IContext
    {

        public string JobQueueUrl { get; set; }
        public int JobQueueMessages { get; set; }

        public bool HasMoreMessages { get; set; }
        public bool HasCapacity { get; set; }
        
        public string StateMachineArn { get; set; }

        public int AvailableCapacity { get; set; }
        
        public int ParallelProcessing { get; set; }

        public int MessagesWaitingForProcessing { get; set; }
        public int MessagesProcessing { get; set; }
        
    }

    public sealed class GetQueueStats : TaskState<SFQueueContext, CheckIfQueueHasMessages>
    {
        IAmazonSQS sqs = new AmazonSQSClient();
        
        public override async Task<SFQueueContext> Execute(SFQueueContext @event)
        {
            var getQueueAttributesResult = await sqs.GetQueueAttributesAsync(new GetQueueAttributesRequest
            {
                QueueUrl = @event.JobQueueUrl,
                AttributeNames = new List<string> { "ApproximateNumberOfMessages", "ApproximateNumberOfMessagesNotVisible" }
            });
            
            @event.AvailableCapacity = @event.ParallelProcessing - getQueueAttributesResult.ApproximateNumberOfMessagesNotVisible;
            @event.MessagesWaitingForProcessing = getQueueAttributesResult.ApproximateNumberOfMessages;
            @event.MessagesProcessing = getQueueAttributesResult.ApproximateNumberOfMessagesNotVisible;

            return @event;
        }
    }

    public sealed class CheckIfQueueHasMessages : ChoiceState<QueueEmpty>
    {
        public override List<Choice> Choices
        {
            get
            {
                return new List<Choice> {
                    new Choice<CheckCapacity, SFQueueContext>(c => c.HasMoreMessages == true)
                };
            }
        }
    }

    public sealed class CheckCapacity : ChoiceState
    {
        public override List<Choice> Choices
        {
            get
            {
                return new List<Choice>{
                    new Choice<StartStepFunctions, SFQueueContext>(c => c.HasCapacity == true)
                };
            }
        }
    }

    public sealed class Wait : WaitState<GetQueueStats> { public override int Seconds => 30; };

    public sealed class StartStepFunctions : TaskState<SFQueueContext, Wait>
    {
        IAmazonStepFunctions stepFunctions = new AmazonStepFunctionsClient();
        IAmazonSQS sqs = new AmazonSQSClient();

        public override async Task<SFQueueContext> Execute(SFQueueContext context)
        {
          

            var maxMessages = context.AvailableCapacity < 10 ?
                context.AvailableCapacity : 10;

            var receiveMessageResult = await sqs.ReceiveMessageAsync(new ReceiveMessageRequest{
                MaxNumberOfMessages = maxMessages,
                QueueUrl = context.JobQueueUrl,
                
            });

            var json = JsonConvert.SerializeObject(new
            {
                // todo: get these from the context object.
                SomeProperty = "some value here.", 
                SomeOtherProperty = 29
            });

            var startExecutionResult = await stepFunctions.StartExecutionAsync(
                new StartExecutionRequest{
                Input = json,
                StateMachineArn = context.StateMachineArn
            });

            Console.WriteLine($"Started step function {context.StateMachineArn}, execution ID: {startExecutionResult.ExecutionArn}");

            context.HasCapacity = false;

            return context;
        }
    }

    public sealed class QueueEmpty : PassState {
        public override bool End => true;
    }
}
