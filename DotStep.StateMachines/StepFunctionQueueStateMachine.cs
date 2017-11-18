using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DotStep.Core;
using Amazon.StepFunctions;
using Amazon.StepFunctions.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;

namespace DotStep.StateMachines.StepFunctionQueue
{
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

        public int MessagesWaitingForProcessing { get; set; }
        public int MessagesProcessing { get; set; }

        public bool QueueHasMessages
        {
            get
            {
                return MessagesWaitingForProcessing > 0 || MessagesProcessing > 0;
            }
        }

        public int AvailableCapacity
        {
            get { return ParallelLevel - MessagesProcessing; }
        }
    }

    public sealed class StepFunctionQueueStateMachine : StateMachine<InitializeContext>
    {
    }

    public sealed class InitializeContext : TaskState<SFQueueContext, GetQueueStats>
    {
        IAmazonSecurityTokenService sts = new AmazonSecurityTokenServiceClient();
        public override async Task<SFQueueContext> Execute(SFQueueContext context)
        {
            CheckRequiredParameters(context);
            var getCallerResult = await sts.GetCallerIdentityAsync(new GetCallerIdentityRequest { });
            context.AccountId = getCallerResult.Account;
            context.Region = Environment.GetEnvironmentVariable("AWS_DEFAULT_REGION") ?? "us-west-2";
            context.JobQueueUrl = $"https://sqs.{context.Region}.amazonaws.com/{context.AccountId}/{context.JobQueueName}";
            context.FileProcessingStateMachineArn = $"arn:aws:states:{context.Region}:{context.AccountId}:stateMachine:{context.FileProcessingStateMachineName}";
            return context;
        }

        private void CheckRequiredParameters(SFQueueContext context)
        {
            if (string.IsNullOrEmpty(context.JobQueueName))
                throw new ArgumentNullException("JobQueueName");
            if (string.IsNullOrEmpty(context.FileProcessingStateMachineName))
                throw new ArgumentNullException("FileProcessingStateMachineName");
            if (string.IsNullOrEmpty(context.EnrichmentEndpoint))
                throw new ArgumentNullException("EnrichmentEndpoint");
            if (string.IsNullOrEmpty(context.DynamoTableName))
                throw new ArgumentNullException("DynamoTableName");
            if (context.ParallelLevel < 1)
                throw new ArgumentException("ParallelLevel must be equal to or greater than 1.");
        }
    }

    public sealed class GetQueueStats : TaskState<SFQueueContext, CheckForMessages>
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

            @event.MessagesWaitingForProcessing = getQueueAttributesResult.ApproximateNumberOfMessages;
            @event.MessagesProcessing = getQueueAttributesResult.ApproximateNumberOfMessagesNotVisible;

            return @event;
        }
    }

    public sealed class CheckForMessages : ChoiceState<QueueEmpty>
    {
        public override List<Choice> Choices
        {
            get
            {
                return new List<Choice> {
                    new Choice<CheckCapacity, SFQueueContext>(c => c.QueueHasMessages == true)
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
            var receiveMessageResult = await sqs.ReceiveMessageAsync(new ReceiveMessageRequest
            {
                MaxNumberOfMessages = maxMessages,
                QueueUrl = context.JobQueueUrl
            });
            var startTasks = new List<Task>();
            foreach (var message in receiveMessageResult.Messages)
            {
                var startTask = stepFunctions.StartExecutionAsync(
                    new StartExecutionRequest
                    {
                        Input = message.Body,
                        StateMachineArn = context.FileProcessingStateMachineArn,
                        Name = message.MessageId
                    });
                startTasks.Add(startTask);
            }
            await Task.WhenAll(startTasks);
            return context;
        }
    }

    public sealed class QueueEmpty : PassState
    {
        public override bool End => true;
    }
}
