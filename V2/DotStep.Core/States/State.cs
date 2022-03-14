namespace DotStep.Core.States
{
    public abstract class State : IState
    {
        protected State()
        {
            // Set default name
            Name = GetType().Name;
        }

        public string Name { get; set; }
        public string Comment { get; set; }
        public dynamic Input { get; set; }
        public dynamic Output { get; set; }
    }
}