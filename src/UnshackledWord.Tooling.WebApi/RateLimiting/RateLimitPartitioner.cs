using System.Net;

namespace UnshackledWord.Tooling.WebApi.RateLimiting;

/// <summary>Pure key functions of the limiter: a null key means "no limit for this request".</summary>
public static class RateLimitPartitioner
{
    public const string UnknownClient = "unknown";
    public const string GeneralTier = "general";

    /// <summary>Health probes (<c>/api/health/*</c>) are never limited: the orchestrator polls them from one address.</summary>
    public static bool IsExemptPath(string? path) =>
        path is not null && (path.Equals("/api/health", StringComparison.OrdinalIgnoreCase) || path.StartsWith("/api/health/", StringComparison.OrdinalIgnoreCase));

    /// <summary>Key of the general tier: every anonymous request counts, signed-in users are exempt.</summary>
    public static string? GeneralKey(bool isAuthenticated, IPAddress? ip) =>
        isAuthenticated ? null : $"{GeneralTier}:{ClientKey(ip)}";

    /// <summary>Key of the strict tier: only anonymous requests of an endpoint in a group; one counter per group.</summary>
    public static string? StrictKey(bool isAuthenticated, string? group, IPAddress? ip) =>
        isAuthenticated || string.IsNullOrEmpty(group) ? null : $"{group}:{ClientKey(ip)}";

    /// <summary>IPv4 as is (mapped IPv6 unwrapped), IPv6 reduced to its /64 prefix so one host cannot rotate through its own range.</summary>
    public static string ClientKey(IPAddress? ip)
    {
        if (ip is null)
        {
            return UnknownClient;
        }

        if (ip.IsIPv4MappedToIPv6)
        {
            ip = ip.MapToIPv4();
        }

        if (ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetworkV6)
        {
            return ip.ToString();
        }

        return Prefix64(ip).ToString() + "/64";
    }

    /// <summary>Anonymised address for log lines: IPv4 with the last octet zeroed, IPv6 as its /64 prefix.</summary>
    public static string ShortenForLog(IPAddress? ip)
    {
        if (ip is null)
        {
            return UnknownClient;
        }

        if (ip.IsIPv4MappedToIPv6)
        {
            ip = ip.MapToIPv4();
        }

        if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
        {
            return Prefix64(ip) + "/64";
        }

        var bytes = ip.GetAddressBytes();
        bytes[^1] = 0;
        return new IPAddress(bytes).ToString();
    }

    private static IPAddress Prefix64(IPAddress ip)
    {
        var bytes = ip.GetAddressBytes();
        Array.Clear(bytes, 8, bytes.Length - 8);
        return new IPAddress(bytes);
    }
}
