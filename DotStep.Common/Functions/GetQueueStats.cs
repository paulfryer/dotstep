using System.Collections.Generic;
using System.Threading.Tasks;
using DotStep.Core;
using Amazon.SQS;
using Amazon.SQS.Model;

namespace DotStep.Common.Functions
{
    public interface IQueueStatsContext : IAccountContext, IRegionContext
    {
        int MessagesWaitingForProcessing { get; set; }
        int MessagesProcessing { get; set; }
        string JobQueueName { get; set; }
        int JobProcessingParallelSize { get; set; }
        int JobProcessingCapacity { get; set; }
        string JobQueueUrl { get; set; }
        bool NoMessagesProcessingOrWaiting { get; set; }
    }



    public sealed class GetQueueStats<TContext> : LambdaFunction<TContext>
        where TContext : IQueueStatsContext
    {
        IAmazonSQS sqs = new AmazonSQSClient();


        public override async Task<TContext> Execute(TContext context)
        {
            {
                context.JobQueueUrl = $"https://sqs.{context.RegionCode}.amazonaws.com/{context.AccountId}/{context.JobQueueName}";

                var getQueueAttributesResult = await sqs.GetQueueAttributesAsync(new GetQueueAttributesRequest
                {
                    QueueUrl = context.JobQueueUrl,
                    AttributeNames = new List<string> {
                    "ApproximateNumberOfMessages",
                    "ApproximateNumberOfMessagesNotVisible" }
                });

                context.MessagesWaitingForProcessing = getQueueAttributesResult.ApproximateNumberOfMessages;
                context.MessagesProcessing = getQueueAttributesResult.ApproximateNumberOfMessagesNotVisible;
                context.JobProcessingCapacity = context.JobProcessingParallelSize - context.MessagesProcessing;
                context.NoMessagesProcessingOrWaiting = context.MessagesWaitingForProcessing == 0 && context.MessagesProcessing == 0;

                return context;
            }
        }
    }
}