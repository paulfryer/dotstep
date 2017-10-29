using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon.CertificateManager;
using Amazon.CertificateManager.Model;
using Amazon.CloudFront;
using Amazon.CloudFront.Model;
using Amazon.Route53;
using Amazon.Route53.Model;
using DotStep.Core;
using System.Linq.Expressions;
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

        public string WorkQueueUrl { get; set; }
    }


    public sealed class GetQueueStats : TaskState<SFQueueContext, CheckIfQueueHasMessages>
    {
        


        public override async Task<SFQueueContext> Execute(SFQueueContext context)
        {
            
            return context;
        }
    }

    public sealed class StartStepFunctions : TaskState<SFQueueContext, Done3>
    {
        IAmazonStepFunctions stepFunctions = new AmazonStepFunctionsClient();

        IAmazonSQS sqs = new AmazonSQSClient();

        public override async Task<SFQueueContext> Execute(SFQueueContext context)
        {
          

            var maxMessages = context.AvailableCapacity < 10 ?
                context.AvailableCapacity : 10;

            var receiveMessageResult = await sqs.ReceiveMessageAsync(new ReceiveMessageRequest{
                MaxNumberOfMessages = maxMessages,
                QueueUrl = context.WorkQueueUrl,
                
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

    public sealed class CheckIfQueueHasMessages : ChoiceState
    {
        public override List<Choice> Choices {
            get {
                return new List<Choice> {
                    new Choice<Done3, SFQueueContext>(c => c.JobQueueMessages < 0),
                    new Choice<Done3, SFQueueContext>(c => c.HasMoreMessages == true)
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
                    new Choice<Done3, SFQueueContext>(c => c.HasCapacity == true)
                };
            }
        }
    }

    public sealed class Done3 : PassState {
        public override bool End => true;
    }


}
