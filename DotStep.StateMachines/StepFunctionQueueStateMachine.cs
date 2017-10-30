using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DotStep.Core;
using Amazon.StepFunctions;
using Amazon.StepFunctions.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using Amazon.IdentityManagement;
using Amazon.IdentityManagement.Model;

namespace DotStep.StateMachines.StepFunctionQueue
{    
    public sealed class StepFunctionQueueStateMachine : StateMachine<InitializeContext>
    {
    }

    public sealed class InitializeContext : TaskState<SFQueueContext, GetQueueStats>
    {

        IAmazonIdentityManagementService iam = new AmazonIdentityManagementServiceClient();

        public override async Task<SFQueueContext> Execute(SFQueueContext context)
        {
            if (String.IsNullOrEmpty(context.Region))
                context.Region = Amazon.Util.EC2InstanceMetadata.Region.SystemName;
            var getUserResult = await iam.GetUserAsync(new GetUserRequest { });
            context.AccountId = getUserResult.User.Arn.Split(':')[4];

            context.JobQueueUrl = $"https://sqs.us-west-2.amazonaws.com/{context.AccountId}/{context.JobQueueName}";
            context.StateMachineArn = $"arn:aws:states:us-west-2:{context.AccountId}:stateMachine:{context.StateMachineName}";


            return context;
        }
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

            @event.AvailableCapacity = @event.ParallelLevel - getQueueAttributesResult.ApproximateNumberOfMessagesNotVisible;
            @event.MessagesWaitingForProcessing = getQueueAttributesResult.ApproximateNumberOfMessages;
            @event.MessagesProcessing = getQueueAttributesResult.ApproximateNumberOfMessagesNotVisible;

            return @event;
        }
    }

    public class SFQueueContext : IContext
    {
        public string Region { get; set; }
        public string JobQueueName { get; set; }
        public string StateMachineName { get; set; }

        public string AccountId { get; internal set; }
        public string JobQueueUrl { get; internal set; }        
        public string StateMachineArn { get; internal set; }

        public int ParallelLevel { get; set; }

        public bool HasMoreMessages { get; internal set; }
        public bool MoreCapacityAvailable { get; internal set; }
        public int AvailableCapacity { get; internal set; }
        public int MessagesWaitingForProcessing { get; internal set; }
        public int MessagesProcessing { get; internal set; }
        
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

    public sealed class CheckCapacity : ChoiceState<Wait>
    {
        public override List<Choice> Choices
        {
            get
            {
                return new List<Choice>{
                    new Choice<StartStepFunctions, SFQueueContext>(c => c.MoreCapacityAvailable == true)
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

            context.MoreCapacityAvailable = false;

            return context;
        }
    }

    public sealed class QueueEmpty : PassState {
        public override bool End => true;
    }
}
