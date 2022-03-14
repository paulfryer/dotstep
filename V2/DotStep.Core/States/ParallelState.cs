using System.Collections.Generic;
using System.Threading.Tasks;

namespace DotStep.Core.States
{
    public class ParallelState : TransitionalState
    {
        public List<IState> States = new List<IState>();

        public override async Task Transition()
        {
            var tasks = new List<Task>();

            foreach (var state in States)
                if (state is TransitionalState transitionalState)
                {
                    // TODO: apply input inputpath filter and transform input into branches.

                    transitionalState.Input = Input;
                    tasks.Add(transitionalState.Transition());
                }


            await Task.WhenAll(tasks);
        }
    }
}