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
    }


    public sealed class GetQueueStats : TaskState<SFQueueContext, CheckIfQueueHasMessages>
    {
        public override async Task<SFQueueContext> Execute(SFQueueContext context)
        {
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

    public sealed class Done3 : PassState {
        public override bool End => true;
    }


}
