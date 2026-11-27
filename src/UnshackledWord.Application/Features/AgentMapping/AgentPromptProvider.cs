using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

namespace UnshackledWord.Application.Features.AgentMapping;

/// <summary>The static prefix of one language: its version is the file name without extension (<c>greek-v1</c>) and is stored with every run.</summary>
public sealed record AgentPrompt(string Version, string Language, string StaticPrefix);

public interface IAgentPromptProvider
{
    /// <summary>The prompt of <c>greek</c> or <c>hebrew</c>; throws <see cref="FileNotFoundException"/> when no file exists.</summary>
    AgentPrompt Get(string language);

    /// <summary>A fixed version (<c>greek-v1</c>): a resumed run keeps the prompt it started with.</summary>
    AgentPrompt GetVersion(string language, string version);
}

/// <summary>
/// Reads <c>assets/agent-prompts/&lt;language&gt;-v&lt;N&gt;.md</c> (newest version unless <c>AgentMapping:PromptVersions:&lt;language&gt;</c> pins one).
/// The text is normalised (LF, trailing whitespace trimmed) and <see cref="AgentAnswerSchema.Placeholder"/> is replaced by the schema, so the
/// result is byte-identical for every package and every run of one version (a changed byte would disable the provider's prefix cache).
/// </summary>
public sealed partial class AgentPromptProvider : IAgentPromptProvider
{
    private static readonly Regex VersionFile = VersionFileRegex();

    private readonly string _directory;
    private readonly IConfiguration? _configuration;

    public AgentPromptProvider(string directory, IConfiguration? configuration = null)
    {
        _directory = directory;
        _configuration = configuration;
    }

    public static AgentPromptProvider FromConfiguration(IConfiguration configuration) => new(ResolveDirectory(configuration), configuration);

    /// <summary>Prefix of the lemma-mode prompt languages (<c>lemma-greek</c>, <c>lemma-hebrew</c>; files <c>lemma-greek-v1.md</c> …, roadmap 0018.9).</summary>
    public const string LemmaPrefix = "lemma-";

    /// <summary>Prefix of the vocab dictionary-form prompt languages (<c>vocab-form-greek</c>, <c>vocab-form-hebrew</c>; roadmap 0022.13).</summary>
    public const string VocabFormPrefix = "vocab-form-";

    /// <summary>Prefix of the vocab root prompt languages (<c>vocab-root-greek</c>, <c>vocab-root-hebrew</c>; roadmap 0030.4).</summary>
    public const string VocabRootPrefix = "vocab-root-";

    /// <summary>Prompt language of the LXX tagging mode (roadmap 0028.7; files <c>lxx-tag-v1.md</c>, <c>lxx-tag-v2.md</c>).</summary>
    public const string LxxTagLanguage = "lxx-tag";

    /// <summary>
    /// The prompt language of a run: <c>greek</c>/<c>hebrew</c> for the mapping modes, <c>lemma-greek</c>/<c>lemma-hebrew</c> for mode <c>lemma</c>,
    /// <c>lxx-tag</c> for mode <c>lxx-tag</c> (any OT book).
    /// </summary>
    public static string LanguageFor(int bibleBookId, AgentMode mode) =>
        mode == AgentMode.LxxTag ? LxxTagLanguage : (mode == AgentMode.Lemma ? LemmaPrefix : string.Empty) + (bibleBookId <= 39 ? "hebrew" : "greek");

    /// <summary>
    /// True when packages and answers of a run with this prompt version carry link roles (roadmap 0018.9): every lemma prompt, and the
    /// mapping prompts from version 2 on (<c>greek-v2</c>). v1 runs keep the v1 package and answer format.
    /// </summary>
    public static bool CarriesRoles(string? promptVersion)
    {
        if (string.IsNullOrWhiteSpace(promptVersion) || VersionFile.Match(promptVersion.Trim() + ".md") is not { Success: true } match)
        {
            return false;
        }

        var language = match.Groups["lang"].Value;

        return language.StartsWith(LemmaPrefix, StringComparison.Ordinal) || (language is "greek" or "hebrew" && int.Parse(match.Groups["n"].Value) >= 2);
    }

    /// <summary>The version <see cref="Get"/> serves for a language right now, or null when no prompt file exists.</summary>
    public static string? CurrentVersion(IAgentPromptProvider prompts, string language)
    {
        try
        {
            return prompts.Get(language).Version;
        }
        catch (FileNotFoundException)
        {
            return null;
        }
    }

    public AgentPrompt GetVersion(string language, string version) => Load(language, Path.Combine(_directory, Path.GetFileName(version) + ".md"));

    public AgentPrompt Get(string language)
    {
        var pinned = _configuration?[$"AgentMapping:PromptVersions:{language}"];
        string? file = null;

        if (!string.IsNullOrWhiteSpace(pinned))
        {
            file = Path.Combine(_directory, pinned.Trim() + ".md");
        }
        else if (Directory.Exists(_directory))
        {
            file = Directory.GetFiles(_directory, $"{language}-v*.md")
                .Select(f => (File: f, Match: VersionFile.Match(Path.GetFileName(f))))
                .Where(x => x.Match.Success && x.Match.Groups["lang"].Value == language)
                .OrderByDescending(x => int.Parse(x.Match.Groups["n"].Value))
                .Select(x => x.File)
                .FirstOrDefault();
        }

        return Load(language, file);
    }

    private AgentPrompt Load(string language, string? file)
    {
        if (file is null || !File.Exists(file))
        {
            throw new FileNotFoundException($"No prompt file for '{language}' in {_directory} (expected {language}-v1.md).");
        }

        var schema = AgentAnswerSchema.ForLanguage(language);
        var text = File.ReadAllText(file).Replace("\r\n", "\n").Replace(AgentAnswerSchema.Placeholder, schema).TrimEnd() + "\n";

        return new AgentPrompt(Path.GetFileNameWithoutExtension(file), language, text);
    }

    /// <summary><c>AgentMapping:PromptsPath</c>, else <c>AppSettings:DatabaseSeeding:SolutionAssetsPath/agent-prompts</c> (the seed assets, mounted in Docker), else <c>assets/agent-prompts</c> above the binaries.</summary>
    public static string ResolveDirectory(IConfiguration configuration)
    {
        var explicitPath = configuration["AgentMapping:PromptsPath"];
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            return Path.GetFullPath(explicitPath);
        }

        var assets = configuration["AppSettings:DatabaseSeeding:SolutionAssetsPath"];
        if (!string.IsNullOrWhiteSpace(assets))
        {
            var candidate = Path.GetFullPath(Path.Combine(assets, "agent-prompts"));
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "assets", "agent-prompts");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        return Path.GetFullPath(Path.Combine(assets ?? "assets", "agent-prompts"));
    }

    [GeneratedRegex(@"^(?<lang>[a-z]+(?:-[a-z]+)*)-v(?<n>\d+)\.md$")]
    private static partial Regex VersionFileRegex();
}
