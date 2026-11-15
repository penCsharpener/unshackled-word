namespace UnshackledWord.Tooling.WebApi.RateLimiting;

/// <summary>Names of the strict-tier groups; each group has its own counter per client.</summary>
public static class RateLimitGroups
{
    public const string Search = "search";
    public const string Auth = "auth";
}

/// <summary>Puts an endpoint into a strict rate-limit group (read by <c>Endpoints.Configurator</c> in Program.cs).</summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class RateLimitGroupAttribute : Attribute
{
    public RateLimitGroupAttribute(string group)
    {
        Group = group;
    }

    public string Group { get; }
}

/// <summary>Endpoint metadata carrying the strict-tier group to the limiter.</summary>
public sealed record RateLimitGroupMetadata(string Group);
