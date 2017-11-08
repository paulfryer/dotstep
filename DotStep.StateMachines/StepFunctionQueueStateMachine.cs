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
            context.FileProcessingStateMachineArn = $"arn:aws:states:us-west-2:{context.AccountId}:stateMachine:{context.FileProcessingStateMachineName}";


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
                AttributeNames = new List<string> {
                    "ApproximateNumberOfMessages",
                    "ApproximateNumberOfMessagesNotVisible" }
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
        public string FileProcessingStateMachineName { get; set; }
        public string DynamoTableName { get; set; }
        public string EnrichmentEndpoint { get; set; }
        public int ParallelLevel { get; set; }

        public string AccountId { get; set; }
        public string JobQueueUrl { get; set; }        
        public string FileProcessingStateMachineArn { get; set; }
        
        //public bool HasMoreMessages { get; set; }
        public int AvailableCapacity { get; set; }
        public int MessagesWaitingForProcessing { get; set; }
        public int MessagesProcessing { get; set; }
    }


    public sealed class CheckIfQueueHasMessages : ChoiceState<QueueEmpty>
    {
        public override List<Choice> Choices
        {
            get
            {
                return new List<Choice> {
                    new Choice<CheckCapacity, SFQueueContext>(c => c.MessagesProcessing > 0 || c.MessagesProcessing > 0)
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
                    new Choice<StartStepFunctions, SFQueueContext>(c => c.AvailableCapacity > 0)
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
                QueueUrl = context.JobQueueUrl                
            });

            var startTasks = new List<Task>();
            foreach (var message in receiveMessageResult.Messages) {
                var json = JsonConvert.SerializeObject(new
                {
                    // todo: get these from the context object.
                    SomeProperty = "some value here.",
                    SomeOtherProperty = 29
                });

                var startTask = stepFunctions.StartExecutionAsync(
                    new StartExecutionRequest
                    {
                        Input = json,
                        StateMachineArn = context.FileProcessingStateMachineArn
                    });

                startTasks.Add(startTask);
            }    
            await Task.WhenAll(startTasks);

            context.AvailableCapacity -= receiveMessageResult.Messages.Count;
            
            return context;
        }
    }

    public sealed class QueueEmpty : PassState {
        public override bool End => true;
    }
}
