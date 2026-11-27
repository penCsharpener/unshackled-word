using System.Text.Encodings.Web;
using System.Text.Json;
using UnshackledWord.Application.Features.Vocab.Forms;

namespace UnshackledWord.Application.Features.Vocab.Roots;

/// <summary>The root a prefill row already has (shown to the model with the run option "Vorbefüllung prüfen").</summary>
public sealed record VocabRootExisting(string Origin, string? Prefixes, string[] PrefixKeys, string Root, string? RootKey);

/// <summary>One entry the model is asked a root for: lemma, main Strong's key, part of speech, first meaning, STEP lexicon text and the existing prefill row.</summary>
public sealed record VocabRootInput(int EntryId, string Lemma, string MainKey, string? Pos, string? Gloss, string? Lexicon, VocabRootExisting? Existing = null, string? Problem = null);

/// <summary>An answered row (not yet validated). <see cref="Simple"/>: the word is its own root.</summary>
public sealed record VocabRootAnswerRow(int EntryId, bool Simple, IReadOnlyList<string> Prefixes, string? RootKey, string? RootLetters, string Confidence, string? Note);

/// <summary>
/// The package and answer of run mode <c>vocab-root</c> (roadmap 0030.4). Package:
/// <c>{"lang":"greek","entries":[["e1","συνεπιμαρτυρέω","G4901","G:V-","bear witness with","&lt;lexicon text&gt;",{"prefixKeys":["G4862"],"rootKey":"G3140"},null], ...]}</c>
/// (local ids <c>e1</c> …, columns <c>[id, lemma, key, pos, gloss, lexicon, existing, problem]</c>, empty columns are <c>null</c>). Answer:
/// <c>{"roots":[{"id":"e1","simple":false,"prefixes":["G4862","G1909"],"rootKey":"G3140","confidence":"high","note":""}]}</c>.
/// </summary>
public static class VocabRootPackage
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string LanguageOf(char track) => AgentMapping.AgentPromptProvider.VocabRootPrefix + (track == 'G' ? "greek" : "hebrew");

    public static string LocalId(int index) => "e" + (index + 1);

    public static string Build(char track, IReadOnlyList<VocabRootInput> entries)
    {
        var rows = entries.Select((e, i) => new object?[]
        {
            LocalId(i),
            e.Lemma,
            e.MainKey,
            e.Pos,
            e.Gloss,
            VocabFormPackage.CleanLexicon(e.Lexicon),
            e.Existing is null ? null : new { prefixKeys = e.Existing.PrefixKeys, rootKey = e.Existing.RootKey, root = e.Existing.Root },
            e.Problem,
        });

        return JsonSerializer.Serialize(new { lang = track == 'G' ? "greek" : "hebrew", entries = rows }, Json);
    }

    /// <summary>Parses the answer; rows with an unknown or duplicate id are dropped. Throws <see cref="JsonException"/> for invalid JSON.</summary>
    public static List<VocabRootAnswerRow> ParseAnswer(string json, IReadOnlyList<VocabRootInput> entries)
    {
        var byLocal = entries.Select((e, i) => (Id: LocalId(i), Entry: e)).ToDictionary(x => x.Id, x => x.Entry.EntryId);
        var seen = new HashSet<string>();
        var rows = new List<VocabRootAnswerRow>();

        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("roots", out var roots) || roots.ValueKind != JsonValueKind.Array)
        {
            return rows;
        }

        foreach (var row in roots.EnumerateArray())
        {
            if (row.ValueKind != JsonValueKind.Object || Text(row, "id") is not { } local
                || !byLocal.TryGetValue(local, out var entryId) || !seen.Add(local))
            {
                continue;
            }

            var prefixes = row.TryGetProperty("prefixes", out var list) && list.ValueKind == JsonValueKind.Array
                ? list.EnumerateArray().Where(p => p.ValueKind == JsonValueKind.String).Select(p => p.GetString()!.Trim()).Where(p => p.Length > 0).ToList()
                : [];
            var simple = row.TryGetProperty("simple", out var flag) && flag.ValueKind == JsonValueKind.True;

            rows.Add(new VocabRootAnswerRow(entryId, simple, prefixes, Text(row, "rootKey"), Text(row, "rootLetters"), Text(row, "confidence") ?? "high", Text(row, "note")));
        }

        return rows;
    }

    private static string? Text(JsonElement row, string name) =>
        row.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && value.GetString() is { } text && !string.IsNullOrWhiteSpace(text)
            ? text.Trim()
            : null;
}
