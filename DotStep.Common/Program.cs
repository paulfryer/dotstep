using Amazon.StepFunctions;
using Amazon.StepFunctions.Model;

using DotStep.Core;
using DotStep.Common.StateMachines;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DotStep.Common
{
    class Program
    {
         
        static void Main(string[] args)
        {
            Test().Wait();
        }

        public static async Task Test() {
            var context = new QueueToStepFunction.Context {
                StateMachineName = "SimpleCalculator-10YP6MNZ2ESA",
                JobQueueName = "tiger-item",
                JobProcessingParallelSize = 10,
                MessageProcessingStateMachineName = "HelloWorldStateMachine-KYZ4RKL7JQVF"
            };
            
            var engine = new StateMachineEngine<QueueToStepFunction, QueueToStepFunction.Context>(context);

            var sm = new QueueToStepFunction();
            var description = sm.Describe("region", "account");

            await engine.Start();
        }
    }
}
