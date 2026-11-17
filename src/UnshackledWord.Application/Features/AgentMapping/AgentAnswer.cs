using System.Text.Json;

namespace UnshackledWord.Application.Features.AgentMapping;

/// <summary><c>[german, source]</c>; from mapping prompt v2 on (roadmap 0018.9) also <c>[german, source, role]</c> and <c>[german, source, role, morpheme]</c>.</summary>
public sealed record AgentAnswerLink(string G, string S, string? Role = null, string? Strongs = null);

public sealed record AgentAnswerAdded(string G, string Parent);

/// <summary><c>[german, lemma]</c>, optionally <c>[german, lemma, stts]</c> and <c>[german, lemma, stts, upos]</c> (the part of speech changes only when the tag needs it).</summary>
public sealed record AgentAnswerLemma(string G, string Lemma, string? Stts, string? Upos);

/// <summary><c>[german, source, order, text]</c>; from mapping prompt v2 on also <c>[german, source, order, text, partLemma]</c>.</summary>
public sealed record AgentAnswerPart(string G, string S, int Order, string Text, string? PartLemma = null);

public sealed record AgentAnswerUnmapped(string G, string Reason);

/// <summary>Lemma mode: <c>[german, source, role]</c> or <c>[german, source, role, morpheme]</c>; role is <c>head</c>, <c>unit</c> or <c>multi-head</c>.</summary>
public sealed record AgentAnswerRole(string G, string S, string Role, string? Strongs);

/// <summary>Lemma mode: <c>[german, partOrder, partLemma]</c>.</summary>
public sealed record AgentAnswerPartLemma(string G, int Order, string Lemma);

public sealed class AgentAnswerVerse
{
    public string V { get; init; } = string.Empty;
    public List<AgentAnswerLink> Links { get; } = [];
    public List<AgentAnswerAdded> Added { get; } = [];
    public List<AgentAnswerLemma> Lemmas { get; } = [];
    public List<AgentAnswerPart> Parts { get; } = [];
    public List<AgentAnswerUnmapped> Unmapped { get; } = [];

    /// <summary>Lemma mode (roadmap 0018): role/morpheme changes of fixed links.</summary>
    public List<AgentAnswerRole> Roles { get; } = [];

    /// <summary>Lemma mode: part lemmas of compound parts.</summary>
    public List<AgentAnswerPartLemma> PartLemmas { get; } = [];

    public string? Note { get; set; }

    /// <summary>Shape problems of this verse (an element that is not the expected array, a missing field); they reject the verse.</summary>
    public List<string> ParseErrors { get; } = [];
}

public sealed class AgentAnswer
{
    public string? PackageId { get; init; }
    public List<AgentAnswerVerse> Verses { get; } = [];

    /// <summary>Problems that make the whole answer unusable (not an object, no <c>verses</c> array).</summary>
    public List<string> Errors { get; } = [];
}

/// <summary>Reads the answer JSON of 04-work-package.md. Lenient about the wrapper (an object or a JSON string holding the object), strict about the rows.</summary>
public static class AgentAnswerParser
{
    public static AgentAnswer Parse(JsonElement answer)
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
                var broken = new AgentAnswer();
                broken.Errors.Add("answer is a string but not valid JSON.");
                return broken;
            }
        }

        string? packageId = null;
        if (answer.ValueKind == JsonValueKind.Object && answer.TryGetProperty("packageId", out var pid) && pid.ValueKind == JsonValueKind.String)
        {
            packageId = pid.GetString();
        }

        var result = new AgentAnswer { PackageId = packageId };

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

    private static AgentAnswerVerse ParseVerse(JsonElement element)
    {
        var verse = new AgentAnswerVerse { V = element.ValueKind == JsonValueKind.Object && Str(element, "v") is { } v ? v : string.Empty };
        if (element.ValueKind != JsonValueKind.Object || verse.V.Length == 0)
        {
            verse.ParseErrors.Add("a verse entry needs a v field with the local verse id.");
            return verse;
        }

        foreach (var row in Rows(element, "links", verse))
        {
            if (Text(row, 0) is { } g && Text(row, 1) is { } s && row.Count is >= 2 and <= 4 && (row.Count < 3 || Text(row, 2) is not null)
                && (row.Count < 4 || row[3].ValueKind is JsonValueKind.String or JsonValueKind.Null))
            {
                verse.Links.Add(new AgentAnswerLink(g, s, row.Count > 2 ? Text(row, 2)!.Trim() : null, row.Count > 3 ? Text(row, 3) : null));
            }
            else
            {
                verse.ParseErrors.Add($"links entry {Show(row)} must be [german, source] or [german, source, role] or [german, source, role, morpheme].");
            }
        }

        foreach (var row in Rows(element, "added", verse))
        {
            if (Text(row, 0) is { } g && Text(row, 1) is { } parent && row.Count == 2)
            {
                verse.Added.Add(new AgentAnswerAdded(g, parent));
            }
            else
            {
                verse.ParseErrors.Add($"added entry {Show(row)} must be [german, parentGerman].");
            }
        }

        foreach (var row in Rows(element, "lemmas", verse))
        {
            if (Text(row, 0) is { } g && Text(row, 1) is { } lemma && row.Count is >= 2 and <= 4)
            {
                verse.Lemmas.Add(new AgentAnswerLemma(g, lemma, row.Count > 2 ? Text(row, 2) : null, row.Count > 3 ? Text(row, 3) : null));
            }
            else
            {
                verse.ParseErrors.Add($"lemmas entry {Show(row)} must be [german, lemma] or [german, lemma, stts].");
            }
        }

        foreach (var row in Rows(element, "parts", verse))
        {
            if (Text(row, 0) is { } g && Text(row, 1) is { } s && Text(row, 3) is { } text && row.Count is 4 or 5 && row[2].ValueKind == JsonValueKind.Number && row[2].TryGetInt32(out var order)
                && (row.Count == 4 || row[4].ValueKind is JsonValueKind.String or JsonValueKind.Null))
            {
                verse.Parts.Add(new AgentAnswerPart(g, s, order, text, row.Count == 5 ? Text(row, 4) : null));
            }
            else
            {
                verse.ParseErrors.Add($"parts entry {Show(row)} must be [german, source, order, text] or [german, source, order, text, partLemma].");
            }
        }

        foreach (var row in Rows(element, "unmapped", verse))
        {
            if (Text(row, 0) is { } g && Text(row, 1) is { } reason && row.Count == 2 && reason.Trim().Length > 0)
            {
                verse.Unmapped.Add(new AgentAnswerUnmapped(g, reason.Trim()));
            }
            else
            {
                verse.ParseErrors.Add($"unmapped entry {Show(row)} must be [german, reason] with a reason.");
            }
        }

        foreach (var row in Rows(element, "roles", verse))
        {
            if (Text(row, 0) is { } g && Text(row, 1) is { } s && Text(row, 2) is { } role && row.Count is 3 or 4 && (row.Count == 3 || row[3].ValueKind is JsonValueKind.String or JsonValueKind.Null))
            {
                verse.Roles.Add(new AgentAnswerRole(g, s, role.Trim(), row.Count == 4 ? Text(row, 3) : null));
            }
            else
            {
                verse.ParseErrors.Add($"roles entry {Show(row)} must be [german, source, role] or [german, source, role, morpheme].");
            }
        }

        foreach (var row in Rows(element, "partLemmas", verse))
        {
            if (Text(row, 0) is { } g && Text(row, 2) is { } lemma && row.Count == 3 && row[1].ValueKind == JsonValueKind.Number && row[1].TryGetInt32(out var order))
            {
                verse.PartLemmas.Add(new AgentAnswerPartLemma(g, order, lemma));
            }
            else
            {
                verse.ParseErrors.Add($"partLemmas entry {Show(row)} must be [german, partOrder, partLemma].");
            }
        }

        if (element.TryGetProperty("note", out var note) && note.ValueKind == JsonValueKind.String)
        {
            verse.Note = note.GetString();
        }

        return verse;
    }

    /// <summary>The array rows of a field; a missing or null field is empty, anything else than an array of arrays is a parse error.</summary>
    private static List<List<JsonElement>> Rows(JsonElement verse, string field, AgentAnswerVerse target)
    {
        var rows = new List<List<JsonElement>>();
        if (!verse.TryGetProperty(field, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return rows;
        }

        if (value.ValueKind != JsonValueKind.Array)
        {
            target.ParseErrors.Add($"{field} must be an array.");
            return rows;
        }

        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Array)
            {
                rows.Add(item.EnumerateArray().ToList());
            }
            else
            {
                target.ParseErrors.Add($"{field} entry {item.GetRawText()} must be an array.");
            }
        }

        return rows;
    }

    private static string? Str(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static string? Text(List<JsonElement> row, int index) =>
        index < row.Count && row[index].ValueKind == JsonValueKind.String ? row[index].GetString() : null;

    private static string Show(List<JsonElement> row) => "[" + string.Join(",", row.Select(e => e.GetRawText())) + "]";
}
