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
        public class Context : AccountRegionContext, IGetExecutionInfoContext
        {
            public string StateMachineName { get; set; }
            public bool AtLeastOneExecutionRunning { get; set;}
        }

        public class EnsureAccountAndRegionAreSet : ReferencedTaskState<Context, Initialize, EnsureAccountAndRegionAreSet<Context>> { }

        public class Initialize : ReferencedTaskState<Context, DetermineExecutionBehavior, GetExecutionInfo<Context>> { }

        public class DetermineExecutionBehavior : ChoiceState<Done>
        {
            public override List<Choice> Choices => new List<Choice>
            {
                new Choice<Done, Context>(c => c.AtLeastOneExecutionRunning == true)
            };
        }

        public class Done : EndState { }

    }
}
