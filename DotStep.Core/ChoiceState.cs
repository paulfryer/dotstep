using System.Collections.Generic;

namespace DotStep.Core
{
    public abstract class ChoiceState : State, IChoiceState
    {
        public abstract List<Choice> Choices { get; }
    }
}
