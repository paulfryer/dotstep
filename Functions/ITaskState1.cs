using System.Threading.Tasks;

namespace DotStep.Core
{
    public interface ITaskState<TContext> : ITaskState
    {
        Task<TContext> Execute(TContext context);
    }
}
