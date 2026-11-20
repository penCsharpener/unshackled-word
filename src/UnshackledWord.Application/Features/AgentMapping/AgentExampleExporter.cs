using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using UnshackledWord.Domain.WebApi.Maintainer;

namespace UnshackledWord.Application.Features.AgentMapping;

public enum AgentExampleOutcome
{
    Ok,
    BadRequest,
    NotFound,
    Conflict,
}

public sealed class AgentExampleResult
{
    public AgentExampleOutcome Outcome { get; init; }
    public string? Error { get; init; }
    public AgentExampleResponse? Example { get; init; }
}

/// <summary>Turns a stored proposal into the package and answer JSON of a worked example for the agent prompt files.</summary>
public interface IAgentExampleExporter
{
    Task<AgentExampleResult> ExportAsync(GetAgentExampleRequest request, int userId, CancellationToken token = default);
}

/// <summary>
/// Builds a one-verse package of the proposal's verse (dry run, so nothing is stored) and writes the proposal as an answer against its hints:
/// a German word whose proposed links equal the hints is left out ("silence keeps a hint"). The package reflects the live data, so the proposal
/// should still be unmerged; after a merge the links are fixed and the example would be empty.
/// </summary>
public sealed class AgentExampleExporter : IAgentExampleExporter
{
    private const string ExampleId = "p_example";
    private const string Todo = "TODO: reason";

    private static readonly JsonSerializerOptions Compact = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly IAgentReviewRepository _proposals;
    private readonly IAgentPackageBuilder _builder;

    public AgentExampleExporter(IAgentReviewRepository proposals, IAgentPackageBuilder builder)
    {
        _proposals = proposals;
        _builder = builder;
    }

    public async Task<AgentExampleResult> ExportAsync(GetAgentExampleRequest request, int userId, CancellationToken token = default)
    {
        if (!AgentModes.TryParse(request.Mode, out var mode) || mode == AgentMode.Lemma)
        {
            return Fail(AgentExampleOutcome.BadRequest, "mode must be redo or fill-open; lemma mode examples are not supported yet.");
        }

        var record = await _proposals.GetAsync(request.Id, token);
        if (record is null)
        {
            return Fail(AgentExampleOutcome.NotFound, $"Proposal {request.Id} does not exist.");
        }

        if (record.RolesJson is not null)
        {
            return Fail(AgentExampleOutcome.BadRequest, "Lemma-mode proposals are not supported yet.");
        }

        var promptVersion = string.IsNullOrWhiteSpace(request.PromptVersion) ? $"{record.Language}-v2" : request.PromptVersion.Trim();
        var maps = new List<AgentPackageMap>();
        var built = await _builder.BuildAsync(new AgentBuildRequest
        {
            Range = new AgentRange { BibleBookId = record.BibleBookId, FromChapter = record.Chapter, FromVerse = record.Verse, ToChapter = record.Chapter, ToVerse = record.Verse },
            Mode = mode,
            UserId = userId,
            DryRun = true,
            PromptVersion = promptVersion,
            Maps = maps,
        }, token);

        if (built is null || built.Packages.Count == 0 || maps.Count == 0)
        {
            return Fail(AgentExampleOutcome.Conflict, "The verse has no open work any more (every word is fixed), so no example package can be built. Export the example before the proposal is merged.");
        }

        var package = built.Packages[0].Package;
        var map = maps[0];
        package.PackageId = ExampleId;

        var warnings = new List<string>();
        if (record.Status != AgentProposalStatuses.Open)
        {
            warnings.Add($"The proposal is {record.Status}; if it was merged, its links are fixed in the live data and the package no longer shows the hints the model saw.");
        }

        var verse = map.Verses["v1"];
        var answerVerse = BuildAnswer(record, verse, map.Roles, warnings);
        var answer = new { packageId = ExampleId, verses = new[] { answerVerse } };

        var packageJson = JsonSerializer.Serialize(package, Compact);
        var answerJson = JsonSerializer.Serialize(answer, Compact);
        var heading = verse.Ref;
        var tag = request.Confirmed ? string.Empty : " [UNCONFIRMED]";
        var markdown = $"## Example: {heading}{tag}\n\nPackage:\n\n```json\n{packageJson}\n```\n\nAnswer:\n\n```json\n{answerJson}\n```\n\nWhat to notice: \n";

        return new AgentExampleResult
        {
            Outcome = AgentExampleOutcome.Ok,
            Example = new AgentExampleResponse
            {
                ProposalId = record.Id,
                ProposalStatus = record.Status,
                Ref = heading,
                PromptVersion = promptVersion,
                PackageJson = packageJson,
                AnswerJson = answerJson,
                Markdown = markdown,
                Warnings = warnings,
            },
        };
    }

    private static AgentExampleResult Fail(AgentExampleOutcome outcome, string error) => new() { Outcome = outcome, Error = error };

    private static Dictionary<string, object?> BuildAnswer(AgentProposalRecord record, AgentVerseMap verse, bool roles, List<string> warnings)
    {
        var links = JsonSerializer.Deserialize<List<UnshackledWord.Domain.WebApi.Elberfelder.GetVerseMapping.VerseMappingLink>>(record.LinksJson, AgentPackageMap.Json) ?? [];
        var lemmas = string.IsNullOrEmpty(record.LemmasJson) ? [] : JsonSerializer.Deserialize<List<AgentLemmaChange>>(record.LemmasJson, AgentPackageMap.Json) ?? [];

        var localOfGerman = verse.German.ToDictionary(g => g.Value.Id, g => g.Key);
        var localOfSource = verse.Source.ToDictionary(s => s.Value.Id, s => s.Key);

        var answerLinks = new List<object?[]>();
        var answerAdded = new List<object?[]>();
        var answerLemmas = new List<object?[]>();
        var answerParts = new List<object?[]>();
        var answerUnmapped = new List<object?[]>();

        foreach (var (g, token) in verse.German)
        {
            if (token.Status == AgentTokenStatus.Fixed)
            {
                continue;
            }

            var proposed = links.Where(l => l.ElbWordId == token.Id).ToList();
            var proposedKeys = new List<(string? S, string? Parent, int? Order, string? Part, string Role, string? Strongs)>();
            foreach (var l in proposed)
            {
                string? s = null;
                string? parent = null;
                if (l.IsAddedWord)
                {
                    if (l.ParentElbWordId is not { } p || !localOfGerman.TryGetValue(p, out parent))
                    {
                        warnings.Add($"{g}: added word with a parent that is not in this verse; skipped.");
                        continue;
                    }
                }
                else if (l.StepWordId is not { } sid || !localOfSource.TryGetValue(sid, out s))
                {
                    warnings.Add($"{g}: link to an original word that is not in this verse; skipped.");
                    continue;
                }

                proposedKeys.Add((s, parent, l.PartOrder, l.GermanWordPart, l.Role ?? MappingRoleHead, l.StrongsNumber));
                AddRow(l, s, parent);
            }

            var hintKeys = verse.Hints.Where(h => h.G == g).Select(h => (h.S, h.Parent, h.PartOrder, h.Part, h.Role ?? MappingRoleHead, h.Strongs)).ToList();
            if (SameSet(proposedKeys, hintKeys))
            {
                // Silence keeps the hints: drop the rows this token just added.
                RemoveRowsOf(g);
            }
            else if (proposedKeys.Count == 0 && proposed.Count == 0)
            {
                answerUnmapped.Add([g, Todo]);
                warnings.Add($"{g}: no link in the proposal; the unmapped reason is a TODO.");
            }

            continue;

            void AddRow(UnshackledWord.Domain.WebApi.Elberfelder.GetVerseMapping.VerseMappingLink l, string? s, string? parent)
            {
                if (l.IsAddedWord)
                {
                    answerAdded.Add([g, parent]);
                }
                else if (l.PartOrder is { } order)
                {
                    answerParts.Add(roles && l.PartLemma is not null ? [g, s, order, l.GermanWordPart, l.PartLemma] : [g, s, order, l.GermanWordPart]);
                }
                else if (roles && (l.StrongsNumber is not null || (l.Role ?? MappingRoleHead) != MappingRoleHead))
                {
                    answerLinks.Add(l.StrongsNumber is null ? [g, s, l.Role] : [g, s, l.Role ?? MappingRoleHead, l.StrongsNumber]);
                }
                else
                {
                    answerLinks.Add([g, s]);
                }
            }

            void RemoveRowsOf(string id)
            {
                answerLinks.RemoveAll(r => (string?)r[0] == id);
                answerAdded.RemoveAll(r => (string?)r[0] == id);
                answerParts.RemoveAll(r => (string?)r[0] == id);
            }
        }

        foreach (var change in lemmas)
        {
            if (!localOfGerman.TryGetValue(change.ElbWordId, out var g) || verse.German[g] is not { } token)
            {
                warnings.Add($"Lemma change of word {change.ElbWordId} is not in this verse; skipped.");
                continue;
            }

            var sttsChanged = change.Stts is not null && change.Stts != token.Stts;
            if (string.Equals(change.Lemma, token.Lemma, StringComparison.Ordinal) && !sttsChanged)
            {
                continue;
            }

            var upos = sttsChanged && change.PartOfSpeech is not null && change.PartOfSpeech != token.Pos ? change.PartOfSpeech : null;
            answerLemmas.Add(upos is not null ? [g, change.Lemma, change.Stts, upos] : sttsChanged ? [g, change.Lemma, change.Stts] : [g, change.Lemma]);
        }

        var result = new Dictionary<string, object?>
        {
            ["v"] = "v1",
            ["links"] = answerLinks,
            ["added"] = answerAdded,
            ["lemmas"] = answerLemmas,
            ["parts"] = answerParts,
            ["unmapped"] = answerUnmapped,
        };
        if (!string.IsNullOrWhiteSpace(record.Note))
        {
            result["note"] = record.Note;
        }

        return result;
    }

    private const string MappingRoleHead = "head";

    private static bool SameSet<T>(List<T> a, List<T> b) => a.Count == b.Count && a.All(x => b.Contains(x));
}
