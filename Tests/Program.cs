using Amazon;
using Amazon.Runtime;
using DotStep.Core;
using DotStep.StateMachines;
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
            TestStateMachine();
            Console.ReadKey();
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
