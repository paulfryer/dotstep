using Amazon;
using Amazon.Runtime;
using DotStep.Core;
using DotStep.Core.Publish;
using DotStep.StateMachines;
using DotStep.StateMachines.CFProxy;
using DotStep.StateMachines.StepFunctionQueue;
using Newtonsoft.Json;
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
            TestStateMachine3();
            Console.ReadKey();
        }

        public static async Task TestStateMachine3()
        {
            IStateMachine stateMachine = new StepFunctionQueueStateMachine();

            var cft = await stateMachine.BuildCloudFormationTemplate();
           

            var context = new PublishContext
            {
                Region = "us-west-2",
                CodeS3Bucket = "code-bucket-here",
                CodeS3Key = "StepFunctionQueueStateMachine.zip",
                CodeStateMachineClassName = "StepFunctionQueueStateMachine"
            };

            var engine = new StateMachineEngine<PublishStateMachine, PublishContext>(context);
            await engine.Start();

        }

        public static async Task TestStateMachine2()
        {
            IStateMachine stateMachine = new StepFunctionQueueStateMachine();

            var context = new SFQueueContext
            {
               JobQueueName = "swift-newFile",
               ParallelLevel = 10,
               StateMachineName = "swift-singleFileProcessor-6"
            };
            
            var engine = new StateMachineEngine<StepFunctionQueueStateMachine, SFQueueContext>(context);
            await engine.Start();

        }

        public static async Task TestStateMachine()
        {
            IStateMachine stateMachine = new CFProxyStateMachine();

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
