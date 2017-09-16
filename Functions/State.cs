namespace DotStep.Core
{
    public abstract class State : IState
    {
        public virtual bool End { get { return false; } }

        public virtual string Name
        {
            get { return this.GetType().Name; }
        }
    }


}
