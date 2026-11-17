using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using UnshackledWord.Application.Features.AgentMapping;
using UnshackledWord.Application.Features.ApiTokens;
using UnshackledWord.Domain.Models.BibleStructure;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Tooling.McpServer.Auth;

namespace UnshackledWord.Tooling.McpServer.Tools;

/// <summary>
/// Agent mapping tools (roadmap 0016.8): an external agent fetches the instructions and a work package, maps one verse range with its own model and
/// submits the answer. Submissions only create proposals; a maintainer merges them. Reading is public; building a stored package and submitting need a
/// maintainer-issued bearer token (<c>Authorization: Bearer uwt_...</c>, scope propose).
/// </summary>
[McpServerToolType]
public static class AgentMappingTools
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    [McpServerTool(Name = "get_agent_instructions", ReadOnly = true)]
    [Description("Returns the rules and the answer JSON schema an agent must follow when it maps German Elberfelder 1871 words to Greek or Hebrew words. Read this once per session before get_mapping_package; it is the same text the server's own worker uses.")]
    public static string GetAgentInstructions(
        IAgentPromptProvider prompts,
        [Description("'greek' (New Testament) or 'hebrew' (Old Testament); 'lemma-greek' / 'lemma-hebrew' for packages of mode 'lemma'.")] string language)
    {
        var lang = language?.Trim().ToLowerInvariant();
        if (lang is not ("greek" or "hebrew" or "lemma-greek" or "lemma-hebrew"))
        {
            throw new McpException("language must be 'greek', 'hebrew', 'lemma-greek' or 'lemma-hebrew'.");
        }

        try
        {
            return prompts.Get(lang).StaticPrefix;
        }
        catch (FileNotFoundException)
        {
            throw new McpException($"No instructions for '{lang}' are installed on this server.");
        }
    }

    [McpServerTool(Name = "get_mapping_package")]
    [Description("Builds the work package(s) for a verse range of one chapter: German words, original words, links that are already decided (fixed), guesses (hints) and what is open, with short local ids (g1, s1). Answer in the format of get_agent_instructions and send it with submit_mapping_proposal. With a valid bearer token the package is stored and bound to the token's user (needed to submit); without a token you get a read-only preview whose packageId cannot be submitted.")]
    public static async Task<string> GetMappingPackage(
        IAgentPackageBuilder builder,
        IMcpCaller caller,
        IConfiguration configuration,
        [Description("Book: BibleBookId 1-66 (Mark = 41), or a German/English name or abbreviation such as 'Mk' or 'Ruth'.")] string book,
        [Description("Chapter number.")] int chapter,
        [Description("First verse.")] int fromVerse,
        [Description("Last verse (inclusive); defaults to fromVerse.")] int? toVerse = null,
        [Description("'redo' (default: decide everything again, existing links come as hints), 'fill-open' (only German words without a mapping) or 'lemma' (links fixed; decide lemmas, link roles, Hebrew morphemes, part lemmas; instructions 'lemma-greek'/'lemma-hebrew').")] string? mode = null,
        CancellationToken cancellationToken = default)
    {
        var options = AgentMappingOptions.From(configuration);
        var bookId = ResolveBook(book);
        var to = toVerse ?? fromVerse;

        if (chapter < 1 || fromVerse < 1 || to < fromVerse)
        {
            throw new McpException("chapter and fromVerse must be at least 1 and toVerse must not be below fromVerse.");
        }

        if (to - fromVerse + 1 > options.McpMaxVersesPerPackageRequest)
        {
            throw new McpException($"At most {options.McpMaxVersesPerPackageRequest} verses per request; ask for a smaller range.");
        }

        if (!AgentModes.TryParse(mode, out var agentMode))
        {
            throw new McpException("mode must be 'redo', 'fill-open' or 'lemma'.");
        }

        var auth = await caller.OptionalProposeAsync(cancellationToken);
        if (auth is { IsValid: false })
        {
            throw new McpException($"auth error: {auth.Message}");
        }

        var response = await builder.BuildAsync(new AgentBuildRequest
        {
            Range = new AgentRange { BibleBookId = bookId, FromChapter = chapter, FromVerse = fromVerse, ToChapter = chapter, ToVerse = to },
            Mode = agentMode,
            UserId = auth?.UserId ?? 0,
            ApiTokenId = auth?.TokenId,
            DryRun = auth is null,

            // The package format follows the prompt get_agent_instructions serves (v2 mapping prompts: links with roles, roadmap 0018.9).
            PromptVersion = AgentPromptProvider.CurrentVersion(AgentPromptProvider.FromConfiguration(configuration), AgentPromptProvider.LanguageFor(bookId, agentMode)),
        }, cancellationToken);

        if (response is null)
        {
            throw new McpException("No German words found for this range.");
        }

        return JsonSerializer.Serialize(new
        {
            submittable = auth is not null,
            note = auth is null ? "Preview only: no token was sent, so nothing was stored and these packageIds cannot be submitted. Send 'Authorization: Bearer <token>' with get_mapping_package to get packages you can submit." : "Answer with get_agent_instructions' format and send each package's answer with submit_mapping_proposal(packageId, answer). Rejected verses can be resubmitted until the package expires.",
            mode = response.Mode,
            stats = response.Stats,
            packages = response.Packages.Select(p => new { p.Package, p.EstimatedTokens, p.ExpiresAt }),
        }, Json);
    }

    [McpServerTool(Name = "lemma_candidates", ReadOnly = true)]
    [Description("German lemma candidates for a word form from the project's lemma lexicon: lemmas people decided (trusted) and automatic ones, with counts. With a verse (e.g. 'Mk 1,29') also the lemma stored for that form in the verse.")]
    public static async Task<string> LemmaCandidates(
        IAgentDataRepository data,
        [Description("The word form as written in the Elberfelder text, e.g. 'gingen'.")] string form,
        [Description("Optional verse reference such as 'Mk 1,29' or 'Joh 3,16'.")] string? verse = null,
        CancellationToken cancellationToken = default)
    {
        var lower = form?.Trim().Trim('.', ',', ';', ':', '!', '?', '·').ToLowerInvariant();
        if (string.IsNullOrEmpty(lower) || lower.Any(char.IsWhiteSpace))
        {
            throw new McpException("form must be a single word.");
        }

        var rows = await data.GetFormLemmasAsync([lower], cancellationToken);
        var lexicon = LemmaLexicon.Build(rows);

        object? inVerse = null;
        if (!string.IsNullOrWhiteSpace(verse))
        {
            var parsed = BibleReferenceParser.Parse(verse);
            if (parsed.Range is null)
            {
                throw new McpException(parsed.Error!);
            }

            var start = BibleReference.FromRefId(parsed.Range.StartRefId);
            var range = await data.GetRangeAsync(start.BookId, start.Chapter, start.Verse, start.Chapter, start.Verse, cancellationToken);
            inVerse = range.GermanWords
                .Where(w => string.Equals(w.Plain, lower, StringComparison.OrdinalIgnoreCase))
                .Select(w => new { position = w.PositionInVerse, word = w.Text, storedLemma = w.Lemma, stts = w.Stts, humanEdited = w.LemmaHumanEdited })
                .ToList();
        }

        return JsonSerializer.Serialize(new
        {
            form = lower,
            trusted = lexicon.TrustedLemmas(lower),
            lemmas = rows.OrderByDescending(r => r.Trusted).ThenByDescending(r => r.Count).Select(r => new { lemma = r.Lemma, r.Count, r.Trusted }),
            inVerse,
        }, Json);
    }

    [McpServerTool(Name = "submit_mapping_proposal", Destructive = false, Idempotent = false)]
    [Description("Submits the answer for one package. Needs 'Authorization: Bearer <token>' (scope propose). Every verse comes back accepted, flagged (stored, a reviewer looks at the flags) or rejected with one message per problem in local ids; fix the rejected verses and submit them again (only those). Nothing is written to live data: a maintainer reviews and merges proposals.")]
    public static async Task<string> SubmitMappingProposal(
        IAgentProposalService proposals,
        IAgentStoreRepository store,
        IAgentRunRepository runs,
        IApiTokenService tokens,
        IMcpCaller caller,
        IConfiguration configuration,
        [Description("packageId from get_mapping_package.")] string packageId,
        [Description("The answer JSON object ({\"verses\":[...]}) or the same as a JSON string.")] JsonElement answer,
        CancellationToken cancellationToken = default)
    {
        var auth = await caller.RequireProposeAsync(cancellationToken);
        if (!auth.IsValid)
        {
            throw new McpException($"auth error: {auth.Message}");
        }

        if (!Guid.TryParse(packageId, out var id))
        {
            throw new McpException($"packageId '{packageId}' is not a package id.");
        }

        // A package is bound to the token (and its user) that asked for it through get_mapping_package.
        if (await store.GetPackageAsync(id, cancellationToken) is { } package)
        {
            var run = await runs.GetRunAsync(package.RunId, cancellationToken);
            if (run is null || run.AiUserId != auth.UserId || run.ApiTokenId != auth.TokenId)
            {
                throw new McpException("This package was not built with your token; call get_mapping_package with the token you submit with.");
            }
        }

        var options = AgentMappingOptions.From(configuration);
        var stored = await tokens.CountProposalsTodayAsync(auth.TokenId, cancellationToken);
        if (stored >= options.McpMaxProposalsPerTokenPerDay)
        {
            throw new McpException($"Daily limit reached: this token has stored {stored} verse proposals today (limit {options.McpMaxProposalsPerTokenPerDay}). Try again tomorrow (UTC).");
        }

        var result = await proposals.SubmitAsync(packageId, answer, cancellationToken);
        if (result.Outcome == AgentSubmitOutcome.Ok)
        {
            return JsonSerializer.Serialize(result.Response, Json);
        }

        throw new McpException($"{result.Outcome}: {string.Join(" ", result.Errors)}");
    }

    private static int ResolveBook(string book)
    {
        var text = book?.Trim() ?? string.Empty;
        if (int.TryParse(text, out var id))
        {
            return id is >= 1 and <= 66 ? id : throw new McpException("book must be 1-66 (BibleBookId) or a book name.");
        }

        var parsed = BibleReferenceParser.Parse($"{text} 1");
        return parsed.Range is null ? throw new McpException(parsed.Error ?? "Unknown book.") : BibleReference.FromRefId(parsed.Range.StartRefId).BookId;
    }
}
