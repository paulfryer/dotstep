using Amazon.StepFunctions;
using Amazon.StepFunctions.Model;
using DotStep.Builder;
using DotStep.Core;
using DotStep.StateMachines.ThottledDynamoWriter;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DotStep.StateMachines
{
    class Program
    {

        static void Main(string[] args)
        {
            Test().Wait();

            //TestStepFunctionDeployer().Wait();
        }

        public static async Task Test() {
            var context = new ThrottledDynamoWriter.Context {
                StateMachineName = "SimpleCalculator-10YP6MNZ2ESA",
                JobQueueName = "tiger-item",
                JobProcessingParallelSize = 10,
                MessageProcessingStateMachineArn = "arn:aws:states:us-west-2:072676109536:stateMachine:HelloWorldStateMachine-KYZ4RKL7JQVF"
            };



            var engine = new StateMachineEngine<ThrottledDynamoWriter, ThrottledDynamoWriter.Context>(context);

            var sm = new ThrottledDynamoWriter();
            var description = sm.Describe("region", "account");

            await engine.Start();
        }


        public static async Task TestStepFunctionDeployer()
        {
            IAmazonStepFunctions stepFunctions = new LocalStepFunctionsService();

            /*
            var context = new SFQueueContext
            {
                Region = "us-west-2",
                DynamoTableName = "stats",
                JobQueueName = "new-file",
                FileProcessingStateMachineName = "SingleFileProcessor",
                EnrichmentEndpoint = "https://www.example.com/endpoint",
                ParallelLevel = 10
            };
            */
            /*
            var startResult = await stepFunctions.StartExecutionAsync(new StartExecutionRequest
            {
                Name = "StepFunctionQueueStateMachine",
                Input = JsonConvert.SerializeObject(context, new JsonSerializerSettings {TypeNameHandling = TypeNameHandling.All })
            });
            */


            //var engine = new StateMachineEngine<StepFunctionQueueStateMachine, SFQueueContext>(context);
            //await engine.Start();

        }
    }
}
