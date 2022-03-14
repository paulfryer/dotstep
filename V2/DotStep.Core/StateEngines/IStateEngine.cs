using System.Threading.Tasks;
using DotStep.Core.StateMachines;

namespace DotStep.Core.StateEngines
{
    public interface IStateEngine
    {
        public Task Run<TStartState>(dynamic input) where TStartState : IStateMachine;
    }
}