using Microsoft.Extensions.Options;

namespace UnshackledWord.Tooling.WebApi.RateLimiting;

/// <summary>Bound from the "RateLimiting" section of appsettings.json (docs/roadmaps/0015-public-rate-limit).</summary>
public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    /// <summary>Off by default: the limiter middleware is not registered at all.</summary>
    public bool Enabled { get; set; }

    /// <summary>Every anonymous request.</summary>
    public RateLimitTierOptions General { get; set; } = new() { PermitLimit = 300, WindowSeconds = 60, SegmentsPerWindow = 6 };

    /// <summary>Expensive search endpoints and auth endpoints (one counter per group).</summary>
    public RateLimitTierOptions Strict { get; set; } = new() { PermitLimit = 20, WindowSeconds = 60, SegmentsPerWindow = 6 };
}

public sealed class RateLimitTierOptions
{
    public int PermitLimit { get; set; }
    public int WindowSeconds { get; set; }
    public int SegmentsPerWindow { get; set; }
}

/// <summary>Fails startup (not the first request) on nonsensical numbers while limiting is enabled.</summary>
public sealed class RateLimitingOptionsValidator : IValidateOptions<RateLimitingOptions>
{
    public ValidateOptionsResult Validate(string? name, RateLimitingOptions options)
    {
        if (!options.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        var errors = new List<string>();
        ValidateTier(nameof(options.General), options.General, errors);
        ValidateTier(nameof(options.Strict), options.Strict, errors);
        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }

    private static void ValidateTier(string tier, RateLimitTierOptions options, List<string> errors)
    {
        var prefix = $"{RateLimitingOptions.SectionName}:{tier}";
        if (options.PermitLimit < 1)
        {
            errors.Add($"{prefix}:PermitLimit must be at least 1 (was {options.PermitLimit}).");
        }

        if (options.WindowSeconds is < 1 or > 3600)
        {
            errors.Add($"{prefix}:WindowSeconds must be between 1 and 3600 (was {options.WindowSeconds}).");
        }

        if (options.SegmentsPerWindow < 1 || options.SegmentsPerWindow > Math.Max(options.WindowSeconds, 1))
        {
            errors.Add($"{prefix}:SegmentsPerWindow must be between 1 and WindowSeconds (was {options.SegmentsPerWindow}).");
        }
    }
}
