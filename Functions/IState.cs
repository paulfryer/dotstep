namespace DotStep.Core
{
    // public interface IPassState : IStat

    public interface IState
    {
        bool End { get; }
        string Name { get; }
    }


}
