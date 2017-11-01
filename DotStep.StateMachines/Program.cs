
using DotStep.Core;
using DotStep.StateMachines.StepFunctionDeployment;
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
            IStateMachine stateMachine = new StepFunctionDeployer();

            var context = new Context
            {
                CodeS3Bucket = "broxy-code",
                CodeS3Key = "H0jfXvX"
            };

            var engine = new StateMachineEngine<StepFunctionDeployer, Context>(context);
            await engine.Start();

        }
    }
}
