using System.Threading.Tasks;

namespace DotStep.Core.States
{
    public interface ITransitionalState : IState
    {
        public IState NextState { get; set; }
        public Task Transition();
    }
}