using System.Text.Json;

namespace UnshackledWord.Application.Features.AgentMapping.Lxx;

/// <summary>
/// One row of an <c>lxx-tag</c> answer, <c>[position, lemma, strongs, morph, heb, confidence]</c>. <see cref="ShapeError"/> is set when the row is
/// not of that shape; <see cref="Position"/> is still read when it can be, so the word is flagged instead of counted as missing.
/// </summary>
public sealed record LxxAnswerWord(int? Position, string? Lemma, string? Strongs, string? Morph, IReadOnlyList<int> Heb, string? Confidence, string? ShapeError = null);

public sealed class LxxAnswerVerse
{
    public string V { get; init; } = string.Empty;
    public List<LxxAnswerWord> Words { get; } = [];
    public string? Note { get; set; }

    /// <summary>Problems of the verse entry itself (no <c>v</c>, no <c>words</c> array); they reject the verse.</summary>
    public List<string> ParseErrors { get; } = [];
}

public sealed class LxxAgentAnswer
{
    public string? PackageId { get; init; }
    public List<LxxAnswerVerse> Verses { get; } = [];

    /// <summary>Problems that make the whole answer unusable.</summary>
    public List<string> Errors { get; } = [];
}

/// <summary>Reads the <c>lxx-tag</c> answer (<see cref="AgentAnswerSchema.LxxJson"/>). Lenient about the wrapper like <see cref="AgentAnswerParser"/>, strict per row.</summary>
public static class LxxAgentAnswerParser
{
    public static LxxAgentAnswer Parse(JsonElement answer)
    {
        if (answer.ValueKind == JsonValueKind.String)
        {
            try
            {
                using var inner = JsonDocument.Parse(answer.GetString() ?? string.Empty);
                return Parse(inner.RootElement.Clone());
            }
            catch (JsonException)
            {
                var broken = new LxxAgentAnswer();
                broken.Errors.Add("answer is a string but not valid JSON.");
                return broken;
            }
        }

        string? packageId = null;
        if (answer.ValueKind == JsonValueKind.Object && answer.TryGetProperty("packageId", out var pid) && pid.ValueKind == JsonValueKind.String)
        {
            packageId = pid.GetString();
        }

        var result = new LxxAgentAnswer { PackageId = packageId };
        if (answer.ValueKind != JsonValueKind.Object || !answer.TryGetProperty("verses", out var verses) || verses.ValueKind != JsonValueKind.Array)
        {
            result.Errors.Add("answer must be an object with a verses array.");
            return result;
        }

        foreach (var verse in verses.EnumerateArray())
        {
            result.Verses.Add(ParseVerse(verse));
        }

        return result;
    }

    private static LxxAnswerVerse ParseVerse(JsonElement element)
    {
        var v = element.ValueKind == JsonValueKind.Object && element.TryGetProperty("v", out var ve) && ve.ValueKind == JsonValueKind.String ? ve.GetString() ?? string.Empty : string.Empty;
        var verse = new LxxAnswerVerse { V = v };
        if (element.ValueKind != JsonValueKind.Object || v.Length == 0)
        {
            verse.ParseErrors.Add("a verse entry needs a v field with the local verse id.");
            return verse;
        }

        if (element.TryGetProperty("note", out var note) && note.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(note.GetString()))
        {
            verse.Note = note.GetString()!.Trim();
        }

        if (!element.TryGetProperty("words", out var words) || words.ValueKind != JsonValueKind.Array)
        {
            verse.ParseErrors.Add("words must be an array of [position, lemma, strongs, morph, heb, confidence] rows.");
            return verse;
        }

        foreach (var row in words.EnumerateArray())
        {
            verse.Words.Add(ParseRow(row));
        }

        return verse;
    }

    private static LxxAnswerWord ParseRow(JsonElement row)
    {
        const string shape = "must be [position, lemma, strongs, morph, heb, confidence]";
        if (row.ValueKind != JsonValueKind.Array)
        {
            return new LxxAnswerWord(null, null, null, null, [], null, $"{Show(row)} {shape}.");
        }

        var cells = row.EnumerateArray().ToList();
        var position = cells.Count > 0 ? Int(cells[0]) : null;
        if (position is null || cells.Count is < 5 or > 6)
        {
            return new LxxAnswerWord(position, null, null, null, [], null, $"{Show(row)} {shape}.");
        }

        var lemma = Str(cells[1]);
        var strongs = Str(cells[2]);
        var morph = Str(cells[3]);
        var heb = new List<int>();
        var hebOk = cells[4].ValueKind is JsonValueKind.Null;
        if (cells[4].ValueKind == JsonValueKind.Array)
        {
            hebOk = true;
            foreach (var h in cells[4].EnumerateArray())
            {
                if (Int(h) is { } n)
                {
                    heb.Add(n);
                }
                else
                {
                    hebOk = false;
                }
            }
        }

        var confidence = cells.Count > 5 ? Str(cells[5]) : null;
        if (lemma is null || strongs is null || morph is null || !hebOk || (cells.Count > 5 && confidence is null && cells[5].ValueKind != JsonValueKind.Null))
        {
            return new LxxAnswerWord(position, lemma, strongs, morph, heb, confidence, $"{Show(row)} {shape} (strings, heb an array of numbers).");
        }

        return new LxxAnswerWord(position, lemma.Trim(), strongs.Trim(), morph.Trim(), heb, confidence?.Trim().ToLowerInvariant());
    }

    private static string? Str(JsonElement e) => e.ValueKind == JsonValueKind.String ? e.GetString() : null;

    private static int? Int(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.Number when e.TryGetInt32(out var n) => n,
        JsonValueKind.String when int.TryParse(e.GetString(), out var n) => n,
        _ => null,
    };

    private static string Show(JsonElement e)
    {
        var text = e.GetRawText();
        return text.Length > 80 ? text[..80] + "…" : text;
    }
}
