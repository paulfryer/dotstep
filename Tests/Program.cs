using Amazon;
using Amazon.Runtime;
using DotStep.Core;
using DotStep.StateMachines;
using DotStep.StateMachines.StepFunctionQueue;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace Tests
{
    class Program
    {

        static void Main(string[] args)
        {
            TestStateMachine2();
            Console.ReadKey();
        }

        public static async Task TestStateMachine2()
        {
            IStateMachine stateMachine = new StepFunctionQueueStateMachine();

            var context = new SFQueueContext
            {
            };

            var engine = new StateMachineEngine<StepFunctionQueueStateMachine, SFQueueContext>(context);

            //var json = stateMachine.Describe("us-west-2", "12345679");

            await engine.Start();


        }

        public static async Task TestStateMachine()
        {
            IStateMachine stateMachine = new CFProxyStateMachine();

            var credentials = Amazon.Util.ProfileManager.GetAWSCredentials("home-dev");

            //await stateMachine.PublishAsync(credentials, "us-west-2", "123456789");

            var context = new CFProxyContext
            {
                DomainName = "testdomain.com",
                Regions = "us-west-1",
                Services = "lambda"
            };

            var engine = new StateMachineEngine<CFProxyStateMachine, CFProxyContext>(context);

            await engine.Start();


        }



    }
}
