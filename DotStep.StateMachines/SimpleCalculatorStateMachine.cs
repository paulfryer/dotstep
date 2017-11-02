using System.Collections.Generic;
using System.Threading.Tasks;
using DotStep.Core;

namespace DotStep.StateMachines.SimpleCalculator
{
    public class Context : IContext
    {
        public int Number1 { get; set; }
        public int Number2 { get; set; }

        public int Product { get; internal set; }
    }

    public sealed class SimpleCalculator: StateMachine<AddNumbers>
    {
    }

    public sealed class AddNumbers : TaskState<Context, DetermineNextStep>
    {
        public override async Task<Context> Execute(Context context)
        {
            context.Product = context.Number1 + context.Number2;   
            return context;
        }
    }

    public sealed class DetermineNextStep : ChoiceState<Wait>
    {
        public override List<Choice> Choices {
            get{
                return new List<Choice>{
                    new Choice<SubtractNumbers, Context>(c => c.Product > 100)
                };
            }
        }
    }

    public sealed class SubtractNumbers : TaskState<Context, Done>
    {
        public override async Task<Context> Execute(Context context)
        {
            context.Product = context.Number1 - context.Number2;
            return context;
        }
    }

    public sealed class Wait : WaitState<Done>
    {
        public override int Seconds => 10;
    }

    public sealed class Done : PassState {
        public override bool End => true;
    }
}