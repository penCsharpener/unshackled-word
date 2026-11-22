using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace UnshackledWord.Application.Features.Vocab.Forms;

/// <summary>One entry the model is asked a dictionary form for: lemma, STEP part of speech, gender and the STEP lexicon line.</summary>
public sealed record VocabFormInput(int EntryId, string Lemma, string? Pos, string? Lexicon);

/// <summary>An answered row: the entry and the form the model proposes (not yet validated).</summary>
public sealed record VocabFormAnswerRow(int EntryId, string Form);

/// <summary>
/// The package and answer of run mode <c>vocab-form</c> (roadmap 0022.13). Package: <c>{"lang":"greek","entries":[["e1","λόγος","G:N-M","M","&lt;lexicon line&gt;"], ...]}</c>
/// (local ids <c>e1</c> …, columns <c>[id, lemma, pos, gender, lexicon]</c>, empty columns are <c>null</c>). Answer: <c>{"forms":[["e1","λόγος, -ου, ὁ"], ...]}</c>.
/// </summary>
public static partial class VocabFormPackage
{
    public const int MaxLexiconLength = 400;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string LanguageOf(char track) => AgentMapping.AgentPromptProvider.VocabFormPrefix + (track == 'G' ? "greek" : "hebrew");

    /// <summary>Gender letter (<c>M</c>, <c>F</c>, <c>N</c>) of a noun morphology such as <c>G:N-M</c>, otherwise null.</summary>
    public static string? GenderOf(string? pos) =>
        pos is not null && GenderRegex().Match(pos) is { Success: true } m ? m.Groups[1].Value : null;

    /// <summary>The lexicon line as plain text on one line, cut to <see cref="MaxLexiconLength"/> characters; null when empty.</summary>
    public static string? CleanLexicon(string? lexicon)
    {
        if (string.IsNullOrWhiteSpace(lexicon))
        {
            return null;
        }

        var text = WhitespaceRegex().Replace(TagRegex().Replace(lexicon, " "), " ").Trim();

        return text.Length == 0 ? null : text.Length > MaxLexiconLength ? text[..MaxLexiconLength] : text;
    }

    public static string LocalId(int index) => "e" + (index + 1);

    public static string Build(char track, IReadOnlyList<VocabFormInput> entries)
    {
        var rows = entries.Select((e, i) => new object?[] { LocalId(i), e.Lemma, e.Pos, GenderOf(e.Pos), CleanLexicon(e.Lexicon) });

        return JsonSerializer.Serialize(new { lang = track == 'G' ? "greek" : "hebrew", entries = rows }, Json);
    }

    /// <summary>Parses the answer; rows with an unknown id, a wrong width or a duplicate id are dropped. Throws <see cref="JsonException"/> for invalid JSON.</summary>
    public static List<VocabFormAnswerRow> ParseAnswer(string json, IReadOnlyList<VocabFormInput> entries)
    {
        var byLocal = entries.Select((e, i) => (Id: LocalId(i), Entry: e)).ToDictionary(x => x.Id, x => x.Entry.EntryId);
        var seen = new HashSet<string>();
        var rows = new List<VocabFormAnswerRow>();

        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("forms", out var forms) || forms.ValueKind != JsonValueKind.Array)
        {
            return rows;
        }

        foreach (var row in forms.EnumerateArray())
        {
            if (row.ValueKind != JsonValueKind.Array || row.GetArrayLength() < 2
                || row[0].ValueKind != JsonValueKind.String || row[1].ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var local = row[0].GetString()!;

            if (byLocal.TryGetValue(local, out var entryId) && seen.Add(local))
            {
                rows.Add(new VocabFormAnswerRow(entryId, row[1].GetString()!));
            }
        }

        return rows;
    }

    [GeneratedRegex(@"^[GH]:N-([MFN])")]
    private static partial Regex GenderRegex();

    [GeneratedRegex("<[^>]*>")]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
