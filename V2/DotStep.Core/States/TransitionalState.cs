using System.Threading.Tasks;

namespace DotStep.Core.States
{
    public abstract class TransitionalState : State, ITransitionalState
    {
        public abstract Task Transition();

        public IState NextState { get; set; }
    }
}