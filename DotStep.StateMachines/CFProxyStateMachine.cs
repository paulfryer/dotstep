using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon.CertificateManager;
using Amazon.CertificateManager.Model;
using Amazon.CloudFront;
using Amazon.CloudFront.Model;
using Amazon.Route53;
using Amazon.Route53.Model;
using DotStep.Core;

namespace DotStep.StateMachines
{
    public sealed class CFProxyStateMachine : StateMachine<ValidateInputParameters>
    {

    }

    public class CFProxyContext : IContext
    {

        public string WAFARN { get; set; }
        public string Regions { get; set; }
        public string Services { get; set; }
        public string DomainName { get; set; }

        public bool CertExists { get; set; }
        public string CertArn { get; set; }
        public bool CertIsApproved { get; set; }

        public int RegionsToProcess { get; set; }
        public int ServicesToProcess { get; set; }
        public string DistributionDomainName { get; set; }
        public string CloudFrontDistributionName { get; set; }
        public string OriginDomainName { get; set; }
        public bool DistributionExists { get; set; }
        public bool CNAMEExists { get; set; }

        public string HostedZoneId { get; set; }       

    }

    public sealed class GetCert : TaskState<CFProxyContext, CheckIfCertExists>
    {

        IAmazonCertificateManager certManager = new AmazonCertificateManagerClient(Amazon.RegionEndpoint.USEast1);


        public override async Task<CFProxyContext> Execute(CFProxyContext e)
        {
            var certs = await certManager.ListCertificatesAsync(
                new ListCertificatesRequest
                {

                }
            );

            // TODO: recursive iterate the results if there is a next token.


            foreach (var cert in certs.CertificateSummaryList)
            {
                if (cert.DomainName.ToLower() == "*." + e.DomainName.ToLower())
                {
                    e.CertExists = true;
                    e.CertArn = cert.CertificateArn;
                }
            }

            return e;
        }
    }

    public sealed class CheckIfCertExists : ChoiceState
    {
        public override List<Choice> Choices
        {
            get
            {
                return new List<Choice>
                {
                    new Choice {
                        Variable = "CertExists",
                        Operator = Operator.BooleanEquals,
                        Value = false,
                        Next = typeof(RequestCert)
                    },
                    new Choice {
                        Variable = "CertExists",
                        Operator = Operator.BooleanEquals,
                        Value = true,
                        Next = typeof(GetCertApprovalStatus)
                    }
                };
            }
        }
    }

    public sealed class CheckIfCertIsApproved : ChoiceState
    {
        public override List<Choice> Choices
        {
            get
            {
                return new List<Choice>
                {
                    new Choice{
                        Variable = "CertIsApproved",
                        Operator = Operator.BooleanEquals,
                        Value = false,
                        Next = typeof(WaitForCertApproval)
                    },
                    new Choice{
                        Variable = "CertIsApproved",
                        Operator = Operator.BooleanEquals,
                        Value = true,
                        Next = typeof(ForEachRegion)
                    }
                };
            }
        }
    }

    public sealed class ForEachRegion : ChoiceState
    {
        public override List<Choice> Choices
        {
            get
            {
                return new List<Choice>{
                    new Choice{
                        Variable = "RegionsToProcess",
                        Operator = Operator.NumericGreaterThan,
                        Value = 0,
                        Next = typeof(ForEachService)
                    },
                    new Choice(){
                        Variable = "RegionsToProcess",
                        Operator = Operator.NumericEquals,
                        Value = 0,
                        Next = typeof(Done)
                    }
                };
            }
        }
    }

    public sealed class ForEachService : ChoiceState
    {
        public override List<Choice> Choices
        {
            get
            {
                return new List<Choice>{
                    new Choice{
                        Variable = "ServicesToProcess",
                        Operator = Operator.NumericGreaterThan,
                        Value = 0,
                        Next = typeof(GetCloudFrontDistribution)
                    },
                    new Choice{
                        Variable = "ServicesToProcess",
                        Operator = Operator.NumericEquals,
                        Value = 0,
                        Next = typeof(ForEachRegion)
                    }
                };
            }
        }
    }

    public sealed class GetCertApprovalStatus : TaskState<CFProxyContext, CheckIfCertIsApproved>
    {
        IAmazonCertificateManager certManager = new AmazonCertificateManagerClient(Amazon.RegionEndpoint.USEast1);

        public override async Task<CFProxyContext> Execute(CFProxyContext e)
        {
            var resp = await certManager.DescribeCertificateAsync(new DescribeCertificateRequest
            {
                CertificateArn = e.CertArn
            });

            if (resp.Certificate.Status == CertificateStatus.PENDING_VALIDATION)
                e.CertIsApproved = false;
            else if (resp.Certificate.Status == CertificateStatus.ISSUED)
                e.CertIsApproved = true;
            else throw new Exception("Unsupported certificate status: " + resp.Certificate.Status);

            return e;
        }
    }

    public sealed class GetCloudFrontDistribution : TaskState<CFProxyContext, CheckIfCloudFrontDistributionExists>
    {
        IAmazonCloudFront cloudFront = new AmazonCloudFrontClient();

        public override async Task<CFProxyContext> Execute(CFProxyContext e)
        {
            var regions = e.Regions.Split(',').ToList();
            var region = regions.First().Trim();
            regions.RemoveAt(0);
            e.Regions = string.Join(",", regions);
            e.RegionsToProcess = regions.Count();

            var services = e.Services.Split(',').ToList();
            var service = services.First().Trim();
            services.RemoveAt(0);
            e.Services = string.Join(",", services);
            e.ServicesToProcess = services.Count();

            e.DistributionDomainName = string.Format($"aws-{region}-{service}.{e.DomainName}");
            e.OriginDomainName = string.Format($"{service}.{region}.amazonaws.com");

            // TODO: implement paging.
            var resp = await cloudFront.ListDistributionsAsync(new ListDistributionsRequest
            {
                MaxItems = "100"
            });


            e.DistributionExists = resp.DistributionList.Items
                .Where(d => d.Aliases.Items.Any())
                .Any(d => d.Aliases.Items.First() == e.DistributionDomainName);

            if (e.DistributionExists)
            {
                foreach (var distribution in resp.DistributionList.Items)
                {
                    if (distribution.Aliases.Items.Contains(e.DistributionDomainName))
                    {
                        e.CloudFrontDistributionName = distribution.DomainName;
                    }
                }
            }

            return e;
        }
    }

    public sealed class CheckIfCloudFrontDistributionExists : ChoiceState
    {
        public override List<Choice> Choices
        {
            get
            {
                return new List<Choice>
                {
                    new Choice{
                        Variable = "DistributionExists",
                        Operator = Operator.BooleanEquals,
                        Value = true,
                        Next = typeof(GetDomainRecords)
                    },
                    new Choice{
                        Variable = "DistributionExists",
                        Operator = Operator.BooleanEquals,
                        Value = false,
                        Next = typeof(CreateCloudFrontDistribution)
                    }
                };
            }
        }
    }

    public sealed class GetDomainRecords : TaskState<CFProxyContext, CheckIfRoute53CNAMEExists>
    {
        IAmazonRoute53 route53 = new AmazonRoute53Client();

        public override async Task<CFProxyContext> Execute(CFProxyContext e)
        {
            var resp = await route53.ListHostedZonesByNameAsync(new ListHostedZonesByNameRequest
            {
            });

            var domainParts = e.DomainName.Split('.');

            var rootDomain = domainParts[domainParts.Length - 2] + "." + domainParts[domainParts.Length - 1];

            var hostedZone = resp.HostedZones.SingleOrDefault(h => h.Name == rootDomain + ".");

            if (hostedZone == null)
            {
                throw new Exception("Could not find hosted zone with domain name: " + e.DomainName);
            }
            else {
                e.HostedZoneId = hostedZone.Id;
            }

            var resp2 = await route53.ListResourceRecordSetsAsync(new ListResourceRecordSetsRequest
            {
                HostedZoneId = hostedZone.Id
            });

            foreach (var recordSet in resp2.ResourceRecordSets)
            {
                if (recordSet.Name == e.DistributionDomainName + ".")
                {
                    e.CNAMEExists = true;
                }
            }

            return e;
        }
    }

    public sealed class CheckIfRoute53CNAMEExists : ChoiceState
    {
        public override List<Choice> Choices
        {
            get
            {
                return new List<Choice>
                {
                    new Choice{
                        Variable = "CNAMEExists",
                        Operator = Operator.BooleanEquals,
                        Value = true,
                        Next = typeof(ForEachService)
                    },
                    new Choice{
                        Variable = "CNAMEExists",
                        Operator = Operator.BooleanEquals,
                        Value = false,
                        Next = typeof(CreateRoute53CNAME)
                    }
                };
            }
        }
    }

    public sealed class CreateRoute53CNAME : TaskState<CFProxyContext, ForEachService>
    {
        public const string CloudFrontZoneId = "Z2FDTNDATAQYW2";

        IAmazonRoute53 route53 = new AmazonRoute53Client();

        public override async Task<CFProxyContext> Execute(CFProxyContext e)
        {
            var resp = await route53.ChangeResourceRecordSetsAsync(new ChangeResourceRecordSetsRequest
            {
                HostedZoneId = e.HostedZoneId,
                ChangeBatch = new ChangeBatch
                {
                    Changes = new List<Change>{
                        new Change{
                            Action = ChangeAction.CREATE,
                            ResourceRecordSet = new ResourceRecordSet{
                                Type = RRType.A,
                                Name = e.DistributionDomainName,
                                AliasTarget = new AliasTarget{                                    
                                    DNSName = e.CloudFrontDistributionName,
                                    EvaluateTargetHealth = false,
                                    HostedZoneId = CloudFrontZoneId
                                }
                            }
                        }
                    }
                }
            });



            return e;
        }
    }

    public sealed class CreateCloudFrontDistribution : TaskState<CFProxyContext, GetDomainRecords>
    {
        IAmazonCloudFront cloudFront = new AmazonCloudFrontClient();

        public override async Task<CFProxyContext> Execute(CFProxyContext e)
        {

            var resp = await cloudFront.CreateDistributionAsync(new CreateDistributionRequest
            {
                DistributionConfig = new DistributionConfig
                {
                    CallerReference = e.DistributionDomainName,
                    Enabled = true,

                    DefaultCacheBehavior = new DefaultCacheBehavior
                    {
                        TrustedSigners = new TrustedSigners
                        {
                            Quantity = 0,
                            Enabled = false
                        },
                        MinTTL = 0,
                        ViewerProtocolPolicy = ViewerProtocolPolicy.HttpsOnly,
                        ForwardedValues = new ForwardedValues
                        {
                            Cookies = new CookiePreference
                            {
                                Forward = ItemSelection.All
                            },
                            Headers = new Headers
                            {
                                Items = new List<string> { "*" },
                                Quantity = 1
                            },
                            QueryString = true,
                            QueryStringCacheKeys = new QueryStringCacheKeys
                            {
                                Quantity = 0
                            }
                        },
                        TargetOriginId = e.OriginDomainName,
                        AllowedMethods = new AllowedMethods
                        {
                            Quantity = 7,
                            Items = new List<string>{
                                "HEAD", "DELETE", "POST", "GET", "OPTIONS", "PUT", "PATCH"
                            },
                            CachedMethods = new CachedMethods
                            {
                                Quantity = 3,
                                Items = new List<string>{
                                    "GET", "HEAD", "OPTIONS"
                                }
                            }
                        }
                    },
                    Comment = string.Format($"AWS Service Proxy. AWS Service: {e.OriginDomainName}, CNAME: {e.DistributionDomainName}"),
                    Aliases = new Aliases
                    {
                        Quantity = 1,
                        Items = new List<string>{
                            e.DistributionDomainName
                        }
                    },
                    Origins = new Origins
                    {

                        Quantity = 1,
                        Items = new List<Origin>{
                            new Origin{

                                CustomOriginConfig = new CustomOriginConfig{
                                    OriginProtocolPolicy = OriginProtocolPolicy.HttpsOnly,
                                    OriginSslProtocols = new OriginSslProtocols{
                                        Items = new List<string>{
                                            "TLSv1.2", "TLSv1.1", "TLSv1"
                                        },
                                        Quantity = 3
                                    },
                                    HTTPPort = 80,
                                    HTTPSPort = 443
                                },
                                DomainName = e.OriginDomainName,
                                Id = e.OriginDomainName
                            }
                        }
                    },
                    ViewerCertificate = new ViewerCertificate
                    {
                        ACMCertificateArn = e.CertArn,
                        SSLSupportMethod = SSLSupportMethod.SniOnly
                    }
                }
            });

            e.CloudFrontDistributionName = resp.Distribution.DomainName;

            return e;
        }
    }

    public sealed class Done : PassState
    {
        public override bool End
        {
            get
            {
                return true;
            }
        }
    }

    public sealed class WaitForCertApproval : WaitState<GetCertApprovalStatus>
    {
        public override int Seconds { get { return 120; } }       

    }

    public sealed class RequestCert : TaskState<CFProxyContext, WaitForCertApproval>
    {
        public override async Task<CFProxyContext> Execute(CFProxyContext context)
        {
            throw new NotImplementedException();
        }
    }

    public sealed class ValidateInputParameters : TaskState<CFProxyContext, GetCert>
    {
        public override async Task<CFProxyContext> Execute(CFProxyContext e)
        {
            if (string.IsNullOrEmpty(e.DomainName))
                throw new ArgumentException("DomainName is required.");

            if (string.IsNullOrEmpty(e.Regions))
                throw new Exception("Regions is required.");

            if (string.IsNullOrEmpty(e.Services))
                throw new ArgumentException("Services is required.");

            e.RegionsToProcess = e.Regions.Split(',').Count();
            e.ServicesToProcess = e.Services.Split(',').Count();

            return e;
        }
    }

}
