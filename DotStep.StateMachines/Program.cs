using Amazon.StepFunctions;
using Amazon.StepFunctions.Model;
using DotStep.Builder;
using DotStep.Core;
using DotStep.StateMachines.StepFunctionDeployment;
using DotStep.StateMachines.StepFunctionQueue;
using Newtonsoft.Json;
using System.Collections.Generic;
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
            IAmazonStepFunctions stepFunctions = new LocalStepFunctionsService();

            var context = new SFQueueContext
            {
                Region = "us-west-2",
                DynamoTableName = "stats",
                JobQueueName = "new-file",
                FileProcessingStateMachineName = "SingleFileProcessor",
                EnrichmentEndpoint = "https://www.example.com/endpoint",
                ParallelLevel = 10
            };

            /*
            var startResult = await stepFunctions.StartExecutionAsync(new StartExecutionRequest
            {
                Name = "StepFunctionQueueStateMachine",
                Input = JsonConvert.SerializeObject(context, new JsonSerializerSettings {TypeNameHandling = TypeNameHandling.All })
            });
            */


            var engine = new StateMachineEngine<StepFunctionQueueStateMachine, SFQueueContext>(context);
            await engine.Start();

        }
    }
}
