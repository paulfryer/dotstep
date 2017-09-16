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

            stateMachine.Publish("us-west-2", "123456789");

        }



    }
}
