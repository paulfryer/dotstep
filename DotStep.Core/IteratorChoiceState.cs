using System.Collections.Generic;

namespace DotStep.Core
{
    public abstract class IteratorChoiceState<TIteration, TDone> : ChoiceState where TIteration : IState where TDone : IState
    {
        public string IteratorVaraible { get; set; }

        public IteratorChoiceState(string iteratorVariable) => IteratorVaraible = iteratorVariable;

        public override List<Choice> Choices
        {
            get
            {
                return new List<Choice>{
                    new Choice{
                        Variable = IteratorVaraible,
                        Operator = Operator.NumericGreaterThan,
                        Value = 0,
                        Next = typeof(TIteration)
                    },
                    new Choice{
                        Variable = IteratorVaraible,
                        Operator = Operator.NumericEquals,
                        Value = 0,
                        Next = typeof(TDone)
                    }
                };
            }
        }
    }
}
