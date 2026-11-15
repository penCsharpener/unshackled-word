using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace UnshackledWord.Tooling.WebApi.RateLimiting;

/// <summary>Bound from the "ReverseProxy" section: which proxies may set X-Forwarded-For. Empty lists: the header is ignored.</summary>
public sealed class ReverseProxyOptions
{
    public const string SectionName = "ReverseProxy";

    /// <summary>Single proxy IPs, e.g. 10.0.0.5. Empty entries (an unset env var) are ignored.</summary>
    public string[] KnownProxies { get; set; } = [];

    /// <summary>CIDR networks, e.g. 172.16.0.0/12 for a proxy on a Docker network. Empty entries are ignored.</summary>
    public string[] KnownNetworks { get; set; } = [];

    public int ForwardLimit { get; set; } = 1;

    public bool HasEntries => Entries(KnownProxies).Any() || Entries(KnownNetworks).Any();

    /// <summary>Builds the middleware options with only the configured proxies trusted (the framework's loopback default is cleared).</summary>
    public ForwardedHeadersOptions ToForwardedHeadersOptions()
    {
        var options = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor,
            ForwardLimit = ForwardLimit < 1 ? 1 : ForwardLimit,
        };
        options.KnownProxies.Clear();
        options.KnownIPNetworks.Clear();

        foreach (var proxy in Entries(KnownProxies))
        {
            if (!IPAddress.TryParse(proxy, out var address))
            {
                throw new InvalidOperationException($"{SectionName}:KnownProxies has an invalid IP address: '{proxy}'.");
            }

            options.KnownProxies.Add(address);
        }

        foreach (var network in Entries(KnownNetworks))
        {
            if (!System.Net.IPNetwork.TryParse(network, out var parsed))
            {
                throw new InvalidOperationException($"{SectionName}:KnownNetworks has an invalid CIDR network: '{network}'.");
            }

            options.KnownIPNetworks.Add(parsed);
        }

        return options;
    }

    private static IEnumerable<string> Entries(string[] values) =>
        values.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim());
}
