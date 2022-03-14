using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DotStep.Core.States
{
    public interface IMapState
    {
        public IState StartState { get; }
    }

    public class MapState<TInput, TIterator, TRequest> : TransitionalState, IMapState
    {
        public List<TInput> Items = new List<TInput>();

        public Func<TInput, List<TIterator>> Iterator;

        public Func<TIterator, TRequest> Mapping;

        public MapState(IState startState)
        {
            StartState = startState;
        }

        public int? MaximumConcurrency { get; set; }

        public IState StartState { get; set; }

        public override async Task Transition()
        {
            Parallel.ForEach(Items, async item =>
            {
                if (StartState is ITransitionalState transitionalState)
                {
                    transitionalState.Input = item;
                    await transitionalState.Transition();
                }
            });
        }
    }
}