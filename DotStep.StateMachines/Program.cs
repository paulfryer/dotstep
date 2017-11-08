
using DotStep.Builder;
using DotStep.Core;
using DotStep.StateMachines.StepFunctionDeployment;
using DotStep.StateMachines.StepFunctionQueue;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

namespace DotStep.StateMachines
{
    class Program
    {

        static void Main(string[] args)
        {
            TestStepFunctionDeployer().Wait();
        }



        public static async Task TestStepFunctionDeployer()
        {
            var context = new SFQueueContext
            {
                Region = "us-west-2",
                DynamoTableName = "stats",
                JobQueueName = "new-file",
                FileProcessingStateMachineName = "SingleFileProcessor",
                EnrichmentEndpoint = "https://www.example.com/endpoint",
                ParallelLevel = 10
            };

            var engine = new StateMachineEngine<StepFunctionQueueStateMachine, SFQueueContext>(context);
            await engine.Start();

        }
    }
}
