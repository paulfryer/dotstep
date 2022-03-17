namespace DotStep.Core.States
{
    public class PassState : State
    {
        public PassState(IState nextState)
        {
            NextState = nextState;
        }

        public IState NextState { get; }
    }
}