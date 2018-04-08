using System;
using System.Linq;
using System.Threading.Tasks;
using DotStep.Core;

namespace DotStep.Common.Functions
{
    public sealed class ValidateMessage<TContext> : LambdaFunction<TContext> where TContext : IContext
    {
        public override async Task<TContext> Execute(TContext context)
        {
            foreach (var propery in context.GetType().GetProperties())
            {
                if (propery.CustomAttributes.All(a => a.AttributeType != typeof(Required))) continue;
                var value = propery.GetValue(context);
                if (value == null)
                    throw new Exception($"Missing required property: {propery.Name}");
            }
            return context;
        }
    }
}