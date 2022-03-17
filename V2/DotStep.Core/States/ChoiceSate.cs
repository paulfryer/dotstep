namespace DotStep.Core.States
{
    public class ChoiceSate<TInput> : State
    {
        public ChoiceSate(IState defaultState)
        {
            DefaultState = defaultState;
        }
        public TInput Input { get; set; }
        public IState DefaultState { get; }
    }
}