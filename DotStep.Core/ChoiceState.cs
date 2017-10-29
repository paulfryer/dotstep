using System;
using System.Collections.Generic;

namespace DotStep.Core
{
    public abstract class ChoiceState : State, IChoiceState
    {
        public abstract List<Choice> Choices { get; }


        public virtual Type Default { get; set; }
    }

    public abstract class ChoiceState<TDefault> : ChoiceState where TDefault : IState
    {
        protected ChoiceState()
        {
            Default = typeof(TDefault);
        }
    }
}
