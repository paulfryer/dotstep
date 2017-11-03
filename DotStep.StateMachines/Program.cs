
using DotStep.Builder;
using DotStep.Core;
using DotStep.StateMachines.StepFunctionDeployment;
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

            

                

            var json = DotStepBuilder.BuildCloudFormationTemplate<StepFunctionDeployer>();


            IStateMachine stateMachine = new StepFunctionDeployer();
            

            var context = new Context
            {
                CodeS3Bucket = "codepipeline-us-west-2-11367317747",
                CodeS3Key = "dotstep-starter/MyAppBuild/qWSuIpz"
            };

            var engine = new StateMachineEngine<StepFunctionDeployer, Context>(context);
            await engine.Start();

        }
    }
}
