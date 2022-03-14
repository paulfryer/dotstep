using DotStep.Core.States;

namespace DotStep.Core.StateMachines
{
    public interface IStateMachine
    {
        public IState GetStartState();
    }
}