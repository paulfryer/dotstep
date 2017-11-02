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

    public sealed class SimpleCalculator//: StateMachine<AddNumbers>
    {
    }

    public sealed class AddNumbers : TaskState<Context, Done>
    {
        public override async Task<Context> Execute(Context context)
        {
            context.Product = context.Number1 + context.Number2;   
            return context;
        }
    }
   
    public sealed class Done : PassState {
        public override bool End => true;
    }
}