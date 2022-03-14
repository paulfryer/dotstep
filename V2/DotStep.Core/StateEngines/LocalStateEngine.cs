using System;
using System.Threading.Tasks;
using DotStep.Core.StateMachines;
using DotStep.Core.States;

namespace DotStep.Core.StateEngines
{
    public class LocalStateEngine : IStateEngine
    {
        public async Task Run<TStartState>(dynamic input) where TStartState : IStateMachine
        {
            var startState = Activator.CreateInstance<TStartState>().GetStartState();
            startState.Input = input;
            if (startState is TransitionalState transitionalState)
                await transitionalState.Transition();
        }
    }
}