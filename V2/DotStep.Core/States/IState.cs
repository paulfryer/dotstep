namespace DotStep.Core.States
{
    public interface IState
    {
        public string Name { get; set; }
        public string Comment { get; set; }

        public dynamic Input { get; set; }

        public dynamic Output { get; set; }
    }
}