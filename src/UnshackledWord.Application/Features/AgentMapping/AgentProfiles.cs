using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace UnshackledWord.Application.Features.AgentMapping;

public static class AgentProviderKinds
{
    public const string Anthropic = "anthropic";
    public const string Gemini = "gemini";

    /// <summary>OpenAI-compatible chat completions (OpenAI, OpenRouter, vLLM, llama.cpp server, LM Studio, Ollama's /v1).</summary>
    public const string OpenAi = "openai";

    /// <summary>Ollama's native <c>/api/chat</c> (local server or Ollama Cloud).</summary>
    public const string Ollama = "ollama";

    public static readonly string[] All = [Anthropic, Gemini, OpenAi, Ollama];
}

/// <summary>How the answer schema is handed to the provider.</summary>
public static class AgentSchemaModes
{
    /// <summary>The provider enforces the schema (<c>output_config.format</c>, <c>responseJsonSchema</c>, <c>response_format</c>, <c>format</c>).</summary>
    public const string Schema = "schema";

    /// <summary>JSON mode only (valid JSON, no schema); the schema stays in the prompt. For providers that reject a schema (Ollama Cloud documents none).</summary>
    public const string Json = "json";

    /// <summary>Nothing is sent; the prompt alone asks for JSON.</summary>
    public const string None = "none";

    public static readonly string[] All = [Schema, Json, None];
}

/// <summary>Prices and caps of one model of a profile; prices are USD per 1M tokens (0 = free or unknown, e.g. a flat-rate plan).</summary>
public sealed class AgentModelSettings
{
    public string Tag { get; init; } = string.Empty;
    public decimal InputPricePerMillion { get; init; }

    /// <summary>Price of cached prompt tokens; when not set it is the input price (no discount assumed).</summary>
    public decimal CachedInputPricePerMillion { get; init; }

    public decimal OutputPricePerMillion { get; init; }

    public long? MaxTokensPerRun { get; init; }
    public decimal? MaxCostPerRun { get; init; }
    public int? MaxVersesPerPackage { get; init; }
}

/// <summary>A provider account (<c>AgentMapping:Profiles:&lt;name&gt;</c>): connection, key, default model and the caps of a run.</summary>
public sealed class AgentProfile
{
    public string Name { get; init; } = string.Empty;
    public string Kind { get; init; } = string.Empty;
    public string BaseUrl { get; init; } = string.Empty;

    /// <summary>Only from secrets or environment, never from appsettings.json; never returned by an endpoint or logged.</summary>
    public string? ApiKey { get; init; }

    public string Model { get; init; } = string.Empty;
    public string SchemaMode { get; init; } = AgentSchemaModes.Schema;

    /// <summary>Packages in flight at the same time after the first (cache warm-up) package.</summary>
    public int MaxParallel { get; init; } = 1;

    public int MaxOutputTokens { get; init; } = 8192;
    public long? MaxTokensPerRun { get; init; }
    public decimal? MaxCostPerRun { get; init; }
    public int? MaxVersesPerPackage { get; init; }
    public IReadOnlyList<AgentModelSettings> Models { get; init; } = [];

    public bool RequiresApiKey => Kind is AgentProviderKinds.Anthropic or AgentProviderKinds.Gemini || !IsLocalHost(BaseUrl);

    public bool HasApiKey => !string.IsNullOrWhiteSpace(ApiKey);

    /// <summary>A profile without a key is usable only when its kind allows it (a local server).</summary>
    public bool IsUsable => HasApiKey || !RequiresApiKey;

    public string? UnusableReason => IsUsable ? null : $"profile '{Name}' has no API key (set AgentMapping:Profiles:{Name}:ApiKey in the secrets file or UNSHACKLEDWORD_AgentMapping__Profiles__{Name}__ApiKey).";

    /// <summary>The settings of a model tag; unknown tags get zero prices and the profile caps.</summary>
    public AgentModelSettings ModelSettings(string tag) =>
        Models.FirstOrDefault(m => AgentProfiles.SameTag(m.Tag, tag)) ?? new AgentModelSettings { Tag = tag };

    public long? EffectiveMaxTokens(string tag) => ModelSettings(tag).MaxTokensPerRun ?? MaxTokensPerRun;

    public decimal? EffectiveMaxCost(string tag) => ModelSettings(tag).MaxCostPerRun ?? MaxCostPerRun;

    public int? EffectiveMaxVerses(string tag) => ModelSettings(tag).MaxVersesPerPackage ?? MaxVersesPerPackage;

    /// <summary>Cost in USD of a call (cached tokens at the cached price, the rest of the input at the input price).</summary>
    public decimal Cost(string tag, long inputTokens, long cachedInputTokens, long outputTokens)
    {
        var m = ModelSettings(tag);
        var cachedPrice = m.CachedInputPricePerMillion > 0 ? m.CachedInputPricePerMillion : m.InputPricePerMillion;
        var cached = Math.Min(Math.Max(cachedInputTokens, 0), Math.Max(inputTokens, 0));
        var fresh = Math.Max(inputTokens, 0) - cached;

        return (fresh * m.InputPricePerMillion + cached * cachedPrice + Math.Max(outputTokens, 0) * m.OutputPricePerMillion) / 1_000_000m;
    }

    public static bool IsLocalHost(string baseUrl)
    {
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri))
        {
            return false;
        }

        var host = uri.Host.ToLowerInvariant();

        return host is "localhost" or "127.0.0.1" or "::1" or "[::1]" || host.EndsWith(".localhost", StringComparison.Ordinal);
    }
}

/// <summary>All profiles of <c>AgentMapping:Profiles</c> and the checks done once at startup.</summary>
public sealed class AgentProfiles
{
    public const string Section = "AgentMapping:Profiles";

    public IReadOnlyList<AgentProfile> Items { get; init; } = [];

    public AgentProfile? Find(string? name) =>
        Items.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));

    public static AgentProfiles From(IConfiguration? configuration)
    {
        if (configuration is null)
        {
            return new AgentProfiles();
        }

        var items = new List<AgentProfile>();
        foreach (var section in configuration.GetSection(Section).GetChildren())
        {
            var kind = (section["Kind"] ?? string.Empty).Trim().ToLowerInvariant();
            var models = section.GetSection("Models").GetChildren().Select(ReadModel).ToList();

            items.Add(new AgentProfile
            {
                Name = section.Key,
                Kind = kind,
                BaseUrl = (section["BaseUrl"] ?? DefaultBaseUrl(kind)).Trim().TrimEnd('/'),
                ApiKey = section["ApiKey"],
                Model = (section["Model"] ?? string.Empty).Trim(),
                SchemaMode = (section["SchemaMode"] ?? AgentSchemaModes.Schema).Trim().ToLowerInvariant(),
                MaxParallel = Int(section["MaxParallel"]) ?? 1,
                MaxOutputTokens = Int(section["MaxOutputTokens"]) ?? 8192,
                MaxTokensPerRun = Long(section["MaxTokensPerRun"]),
                MaxCostPerRun = Dec(section["MaxCostPerRun"]),
                MaxVersesPerPackage = Int(section["MaxVersesPerPackage"]),
                Models = models,
            });
        }

        return new AgentProfiles { Items = items };
    }

    /// <summary>Structural problems that make the configuration wrong (not a missing key: see <see cref="AgentProfile.IsUsable"/>).</summary>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        foreach (var p in Items)
        {
            var at = $"AgentMapping:Profiles:{p.Name}";
            if (!AgentProviderKinds.All.Contains(p.Kind))
            {
                errors.Add($"{at}:Kind '{p.Kind}' is not one of {string.Join(", ", AgentProviderKinds.All)}.");
            }

            if (!Uri.TryCreate(p.BaseUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            {
                errors.Add($"{at}:BaseUrl '{p.BaseUrl}' is not an http(s) URL.");
            }

            if (!AgentSchemaModes.All.Contains(p.SchemaMode))
            {
                errors.Add($"{at}:SchemaMode '{p.SchemaMode}' is not one of {string.Join(", ", AgentSchemaModes.All)}.");
            }

            if (p.Model.Length == 0)
            {
                errors.Add($"{at}:Model is missing (the default model tag).");
            }

            if (p.MaxParallel is < 1 or > 16)
            {
                errors.Add($"{at}:MaxParallel must be 1..16.");
            }

            if (p.MaxOutputTokens < 256)
            {
                errors.Add($"{at}:MaxOutputTokens must be at least 256.");
            }

            if (p.MaxTokensPerRun is <= 0 || p.MaxCostPerRun is <= 0 || p.MaxVersesPerPackage is <= 0)
            {
                errors.Add($"{at}: MaxTokensPerRun, MaxCostPerRun and MaxVersesPerPackage must be positive when set.");
            }

            foreach (var m in p.Models)
            {
                if (m.Tag.Length == 0 || m.InputPricePerMillion < 0 || m.CachedInputPricePerMillion < 0 || m.OutputPricePerMillion < 0
                    || m.MaxTokensPerRun is <= 0 || m.MaxCostPerRun is <= 0 || m.MaxVersesPerPackage is <= 0)
                {
                    errors.Add($"{at}:Models:'{m.Tag}' has a negative price or a non-positive cap.");
                }
            }
        }

        return errors;
    }

    /// <summary>Tags compare ignoring <c>:</c> <c>/</c> <c>.</c> <c>-</c> differences between a tag and a configuration key (<c>gemma4:31b</c> is the key <c>gemma4_31b</c>).</summary>
    public static bool SameTag(string a, string b) => Normalize(a) == Normalize(b);

    private static string Normalize(string tag) => tag.Trim().ToLowerInvariant().Replace(':', '_').Replace('/', '_');

    private static AgentModelSettings ReadModel(IConfigurationSection s) => new()
    {
        Tag = (s["Tag"] ?? s.Key).Trim(),
        InputPricePerMillion = Dec(s["InputPricePerMillion"]) ?? 0,
        CachedInputPricePerMillion = Dec(s["CachedInputPricePerMillion"]) ?? 0,
        OutputPricePerMillion = Dec(s["OutputPricePerMillion"]) ?? 0,
        MaxTokensPerRun = Long(s["MaxTokensPerRun"]),
        MaxCostPerRun = Dec(s["MaxCostPerRun"]),
        MaxVersesPerPackage = Int(s["MaxVersesPerPackage"]),
    };

    private static string DefaultBaseUrl(string kind) => kind switch
    {
        AgentProviderKinds.Anthropic => "https://api.anthropic.com",
        AgentProviderKinds.Gemini => "https://generativelanguage.googleapis.com",
        AgentProviderKinds.OpenAi => "https://api.openai.com/v1",
        AgentProviderKinds.Ollama => "https://ollama.com",
        _ => string.Empty,
    };

    private static int? Int(string? text) => int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null;

    private static long? Long(string? text) => long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null;

    private static decimal? Dec(string? text) => decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;
}
