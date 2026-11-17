using Microsoft.Extensions.Configuration;

namespace UnshackledWord.Application.Features.AgentMapping;

/// <summary>Settings of the agent mapping core, section <c>AgentMapping</c> (every value is optional).</summary>
public sealed class AgentMappingOptions
{
    public const string Section = "AgentMapping";

    /// <summary>Budget of the variable part of one package in estimated tokens (04-work-package.md: ~6,000).</summary>
    public int TokenBudget { get; init; } = 6000;

    /// <summary>Characters per estimated token for ASCII text; non-ASCII characters (Greek, Hebrew, umlauts) count double.</summary>
    public double CharsPerToken { get; init; } = 3.5;

    /// <summary>Hours until a package can no longer be submitted.</summary>
    public int PackageExpiryHours { get; init; } = 24;

    /// <summary>K of the lemma-to-Strongs statistics rule: confirmed links needed before a single candidate becomes a hint.</summary>
    public int MinConfirmations { get; init; } = 3;

    /// <summary>Confirmed links needed before a proper name counts as having a known German form.</summary>
    public int NameMinConfirmations { get; init; } = 1;

    /// <summary>Daily cap of stored verse proposals per MCP token (UTC day), key <c>AgentMapping:McpMaxProposalsPerTokenPerDay</c>.</summary>
    public int McpMaxProposalsPerTokenPerDay { get; init; } = 300;

    /// <summary>Most verses one <c>get_mapping_package</c> call may ask for, key <c>AgentMapping:McpMaxVersesPerPackageRequest</c>.</summary>
    public int McpMaxVersesPerPackageRequest { get; init; } = 30;

    public static AgentMappingOptions From(IConfiguration? configuration)
    {
        var defaults = new AgentMappingOptions();
        if (configuration is null)
        {
            return defaults;
        }

        return new AgentMappingOptions
        {
            TokenBudget = Int(configuration, nameof(TokenBudget), defaults.TokenBudget),
            CharsPerToken = double.TryParse(configuration[$"{Section}:{nameof(CharsPerToken)}"], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var c) && c > 0 ? c : defaults.CharsPerToken,
            PackageExpiryHours = Int(configuration, nameof(PackageExpiryHours), defaults.PackageExpiryHours),
            MinConfirmations = Int(configuration, nameof(MinConfirmations), defaults.MinConfirmations),
            NameMinConfirmations = Int(configuration, nameof(NameMinConfirmations), defaults.NameMinConfirmations),
            McpMaxProposalsPerTokenPerDay = Int(configuration, nameof(McpMaxProposalsPerTokenPerDay), defaults.McpMaxProposalsPerTokenPerDay),
            McpMaxVersesPerPackageRequest = Int(configuration, nameof(McpMaxVersesPerPackageRequest), defaults.McpMaxVersesPerPackageRequest),
        };
    }

    private static int Int(IConfiguration configuration, string key, int fallback) =>
        int.TryParse(configuration[$"{Section}:{key}"], out var value) && value > 0 ? value : fallback;
}

public enum AgentMode
{
    /// <summary>Only German words without a mapping are asked; existing links are context (<c>fixed</c>).</summary>
    FillOpen,

    /// <summary>Everything is re-decided; existing human-confirmed links come back as hints.</summary>
    Redo,

    /// <summary>
    /// Roadmap 0018: links are fixed; only lemma, STTS, link role, Hebrew morpheme, part lemma and "subject pronoun → added word" are decided.
    /// Only verses with work (06-ai-runs.md) are sent.
    /// </summary>
    Lemma,
}

public static class AgentModes
{
    public const string FillOpen = "fill-open";
    public const string Redo = "redo";
    public const string Lemma = "lemma";

    public static string ToDb(this AgentMode mode) => mode switch
    {
        AgentMode.Redo => Redo,
        AgentMode.Lemma => Lemma,
        _ => FillOpen,
    };

    public static bool TryParse(string? text, out AgentMode mode)
    {
        mode = AgentMode.Redo;
        switch (text?.Trim().ToLowerInvariant())
        {
            case null or "" or Redo:
                return true;
            case FillOpen:
                mode = AgentMode.FillOpen;
                return true;
            case Lemma:
                mode = AgentMode.Lemma;
                return true;
            default:
                return false;
        }
    }
}
