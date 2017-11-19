using System.Threading.Tasks;
using DotStep.Core;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;
using System;

namespace DotStep.Common.Functions
{
    public sealed class EnsureAccountAndRegionAreSet<TContext> : LambdaFunction<TContext>
    where TContext : IRegionContext, IAccountContext
    {
        public override async Task<TContext> Execute(TContext context)
        {
            if (string.IsNullOrEmpty(context.AccountId))
            {
                IAmazonSecurityTokenService sts = new AmazonSecurityTokenServiceClient();
                var getCallerResult = await sts.GetCallerIdentityAsync(new GetCallerIdentityRequest { });
                context.AccountId = getCallerResult.Account;
            }
            if (string.IsNullOrEmpty(context.RegionCode))
                context.RegionCode = Environment.GetEnvironmentVariable("AWS_DEFAULT_REGION") ?? "us-west-2";
            return context;
        }
    }
}
