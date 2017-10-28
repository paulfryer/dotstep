using System.Collections.Generic;

namespace DotStep.Core
{
    public abstract class ChoiceState : State, IChoiceState
    {
        public abstract List<Choice> Choices { get; }

        // TODO: Implement Default.....

        //public string Default
    }

   // public abstract class ChoiceState<TContext> : ChoiceState where TContext : IContext {

   // }
}
