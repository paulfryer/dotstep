using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon.SQS;
using Amazon.SQS.Model;
using Amazon.StepFunctions;
using Amazon.StepFunctions.Model;
using DotStep.Core;

namespace DotStep.Common.Functions
{
    public interface IMessageProcessingContext
    {
        string MessageProcessingStateMachineName { get; set; }
        string MessageProcessingStateMachineArn { get; set; }
    }

    public sealed class ProcessMessages<TContext> : LambdaFunction<TContext>
     where TContext : IQueueStatsContext, IMessageProcessingContext
    {

        IAmazonStepFunctions stepFunctions = new AmazonStepFunctionsClient();
        IAmazonSQS sqs = new AmazonSQSClient();

        public override async Task<TContext> Execute(TContext context)
        {
            context.MessageProcessingStateMachineArn = $"arn:aws:states:{context.RegionCode}:{context.AccountId}:stateMachine:{context.MessageProcessingStateMachineName}";

            var maxMessages = context.JobProcessingCapacity < 10 ?
                context.JobProcessingCapacity : 10;
            var receiveMessageResult = await sqs.ReceiveMessageAsync(new ReceiveMessageRequest
            {
                MaxNumberOfMessages = maxMessages,
                QueueUrl = context.JobQueueUrl
            });

            if (receiveMessageResult.Messages.Count > 0) {
                var startTasks = new List<Task>();
                foreach (var message in receiveMessageResult.Messages)
                {
                    var startTask = stepFunctions.StartExecutionAsync(
                        new StartExecutionRequest
                        {
                            Input = message.Body,
                            StateMachineArn = context.MessageProcessingStateMachineArn,
                            Name = message.MessageId
                        });
                    startTasks.Add(startTask);
                }
                await Task.WhenAll(startTasks);

                var deleteResult = await sqs.DeleteMessageBatchAsync(new DeleteMessageBatchRequest
                {
                    QueueUrl = context.JobQueueUrl,
                    Entries = receiveMessageResult.Messages.Select(message => new DeleteMessageBatchRequestEntry
                    {
                        Id = message.MessageId,
                        ReceiptHandle = message.ReceiptHandle
                    }).ToList()
                });
            }

            return context;
        }

    }
}