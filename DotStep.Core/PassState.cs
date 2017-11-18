namespace DotStep.Core
{
    public abstract class PassState : State, IPassState {
    }

    public abstract class EndState : PassState {
        public override bool End => true;
    }
    
}
