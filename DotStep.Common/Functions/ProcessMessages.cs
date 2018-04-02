using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon.Lambda;
using Amazon.Lambda.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using Amazon.StepFunctions;
using Amazon.StepFunctions.Model;
using DotStep.Core;

namespace DotStep.Common.Functions
{
    public interface IMessageProcessingContext
    {
        string MessageProcessorType { get; set; }
        string MessageProcessorName { get; set; }
    }

    public sealed class ProcessMessages<TContext> : LambdaFunction<TContext>
        where TContext : IQueueStatsContext, IMessageProcessingContext
    {
        readonly IAmazonLambda lambda = new AmazonLambdaClient();
        readonly IAmazonSQS sqs = new AmazonSQSClient();

        readonly IAmazonStepFunctions stepFunctions = new AmazonStepFunctionsClient();

        public override async Task<TContext> Execute(TContext context)
        {
            var maxMessages = context.JobProcessingCapacity < 10 ? context.JobProcessingCapacity : 10;
            var receiveMessageResult = await sqs.ReceiveMessageAsync(new ReceiveMessageRequest
            {
                MaxNumberOfMessages = maxMessages,
                QueueUrl = context.JobQueueUrl
            });

            if (receiveMessageResult.Messages.Count > 0)
            {
                var tasks = new List<Task>();
                foreach (var message in receiveMessageResult.Messages)
                {
                    Task task = null;
                    switch (context.MessageProcessorType)
                    {
                        case "Lambda":
                            var lambdaArn =
                                $"arn:aws:{context.RegionCode}:{context.AccountId}:function:{context.MessageProcessorName}";
                            task = lambda.InvokeAsync(
                                new InvokeRequest
                                {
                                    FunctionName = context.MessageProcessorName,
                                    Payload = message.Body,
                                    InvocationType = InvocationType.Event,
                                    ClientContext = GetType().Name
                                });
                            break;
                        case "StepFunction":
                            task = stepFunctions.StartExecutionAsync(
                                new StartExecutionRequest
                                {
                                    Input = message.Body,
                                    StateMachineArn =
                                        $"arn:aws:states:{context.RegionCode}:{context.AccountId}:stateMachine:{context.MessageProcessorName}",
                                    Name = message.MessageId
                                });
                            break;
                        default:
                            throw new Exception($"Unsupported MessageProcessorType: {context.MessageProcessorType}");
                    }

                    tasks.Add(task);
                }

                await Task.WhenAll(tasks);

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