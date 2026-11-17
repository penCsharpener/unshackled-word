using System.Text.Json;
using UnshackledWord.Domain.WebApi.Maintainer;

namespace UnshackledWord.Application.Features.AgentMapping.Scoring;

public sealed class AgentFreezeResult
{
    public bool Ok { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public ReferenceSet? Reference { get; init; }
}

public interface IAgentReferenceFreezer
{
    /// <summary>Exports the current database state of the verses as a reference set. Fails for prompt example verses, duplicates and unknown verses.</summary>
    Task<AgentFreezeResult> FreezeAsync(FreezeAgentReferenceRequest request, CancellationToken token = default);
}

/// <summary>
/// "Freeze reference" (roadmap 0018.10): the maintainer reviews the verses in the editor first, then this writes what the database says now into the
/// reference format. The result is a file the maintainer commits as <c>assets/agent-reference/reference-vN.json</c>; this class never writes files.
/// </summary>
public sealed class AgentReferenceFreezer : IAgentReferenceFreezer
{
    private readonly IAgentDataRepository _data;
    private readonly TimeProvider _time;

    public AgentReferenceFreezer(IAgentDataRepository data, TimeProvider time)
    {
        _data = data;
        _time = time;
    }

    public async Task<AgentFreezeResult> FreezeAsync(FreezeAgentReferenceRequest request, CancellationToken token = default)
    {
        var errors = new List<string>();
        var version = request.Version?.Trim() ?? string.Empty;
        if (version.Length == 0 || !version.All(c => char.IsLetterOrDigit(c) || c is '-' or '_'))
        {
            errors.Add("version must be letters, digits, - or _ (for example v1).");
        }

        if (request.Verses.Count == 0)
        {
            errors.Add("verses is empty.");
        }

        var keys = new List<(int Book, int Chapter, int Verse)>();
        foreach (var verse in request.Verses)
        {
            var name = AgentVerseSnapshots.RefText(verse.BibleBookId, verse.Chapter, verse.Verse);
            if (verse.BibleBookId is < 1 or > 66 || verse.Chapter < 1 || verse.Verse < 1)
            {
                errors.Add($"{name}: not a valid verse.");
            }
            else if (ReferenceSetFile.IsPromptExample(verse.BibleBookId, verse.Chapter, verse.Verse))
            {
                errors.Add($"{name}: is a worked example of the prompts and must not be in the reference set.");
            }
            else if (keys.Contains((verse.BibleBookId, verse.Chapter, verse.Verse)))
            {
                errors.Add($"{name}: listed twice.");
            }
            else
            {
                keys.Add((verse.BibleBookId, verse.Chapter, verse.Verse));
            }
        }

        if (errors.Count > 0)
        {
            return new AgentFreezeResult { Errors = errors };
        }

        var set = new ReferenceSet { Version = version, FrozenAt = _time.GetUtcNow() };
        var warnings = new List<string>();
        foreach (var (book, chapter, verse) in keys.OrderBy(k => k.Book).ThenBy(k => k.Chapter).ThenBy(k => k.Verse))
        {
            var data = await _data.GetRangeAsync(book, chapter, verse, chapter, verse, token);
            var snapshot = AgentVerseSnapshots.Build(book, chapter, verse, data);
            if (snapshot.State.Words.Count == 0)
            {
                errors.Add($"{AgentVerseSnapshots.RefText(book, chapter, verse)}: the verse does not exist.");
                continue;
            }

            set.Verses.Add(snapshot.State);
            warnings.AddRange(snapshot.Warnings);
        }

        if (errors.Count == 0)
        {
            errors.AddRange(ReferenceSetFile.Validate(set));
        }

        return errors.Count > 0
            ? new AgentFreezeResult { Errors = errors, Warnings = warnings }
            : new AgentFreezeResult { Ok = true, Reference = set, Warnings = warnings };
    }

    public static JsonElement ToElement(ReferenceSet set) => JsonDocument.Parse(ReferenceSetFile.Serialize(set)).RootElement.Clone();
}
