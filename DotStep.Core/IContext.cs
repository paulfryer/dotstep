namespace DotStep.Core
{
    public interface IContext
    {

    }

    public interface IRegionContext : IContext {
        string RegionCode { get; set; }
    }

    public interface IAccountContext : IContext {
        string AccountId { get; set; }
    }

    public class AccountRegionContext : IAccountContext, IRegionContext {
        public string AccountId { get; set; }
        public string RegionCode { get; set; }

    };
}
