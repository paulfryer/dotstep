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
            TestCFQuickStart().Wait();
        }

        public static async Task TestCFQuickStart() {

            var context = new CFQuickStartStateMachine.Context
            {
               ProjectName = "dotstep-starter",
               SourceCodeDirectory = "dotstep-starter-master",
               ProjectZipLocation = "https://github.com/paulfryer/dotstep-starter/archive/master.zip"
            };

            var engine = new StateMachineEngine<CFQuickStartStateMachine, CFQuickStartStateMachine.Context>(context);
            var sm = new CFQuickStartStateMachine();
            await engine.Start();

        }


        public static async Task TestThrottledProcessor() {

            var lambda = new DotStep.Common.StateMachines.ThrottledProcessor.EnsureAccountAndRegionAreSet();

            var type = lambda.GetType();


            var context = new ThrottledProcessor.Context {
                StateMachineName = "QueueToStepFunction-mIdf0XJZ3l94",
                JobQueueName = "tiger-item",
                JobProcessingParallelSize = 10,
                MessageProcessorName = "write-event-test",
                MessageProcessorType = "Lambda"
            };
            
            var engine = new StateMachineEngine<ThrottledProcessor, ThrottledProcessor.Context>(context);

            var sm = new ThrottledProcessor();
            var description = sm.Describe("region", "account");

            await engine.Start();
        }
    }
}
