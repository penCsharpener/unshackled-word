using System.ComponentModel;
using System.Text;
using ModelContextProtocol.Server;
using UnshackledWord.Application.Repositories;
using UnshackledWord.Domain.Models.BibleStructure;

namespace UnshackledWord.Tooling.McpServer.Tools;

[McpServerToolType]
public static class OriginalTextTools
{
    public const int MaxVerses = 100;

    [McpServerTool(Name = "get_original_text", ReadOnly = true)]
    [Description("Returns the original-language text of a passage from the STEP Bible data: Hebrew/Aramaic for the Old Testament, Greek for the New Testament, one line per verse. Words are lowercased; optionally adds a lemma line.")]
    public static async Task<string> GetOriginalText(
        IStepBibleVerseRepository verses,
        [Description("Bible reference in German or English notation (Elberfelder verse numbering), e.g. '1Mo 1,1-3', 'Ps 23', 'Joh 3,16'.")] string reference,
        [Description("Also return the lemmas (dictionary forms, without diacritics) of each verse. Default false.")] bool includeLemmas = false,
        CancellationToken cancellationToken = default)
    {
        var parsed = BibleReferenceParser.Parse(reference);
        if (parsed.Range is null)
        {
            return parsed.Error!;
        }

        var range = parsed.Range;
        var rows = await verses.GetByHebRefIdRangeAsync(range.StartRefId, range.EndRefId, MaxVerses + 1, cancellationToken);
        if (rows.Count == 0)
        {
            return $"No verses found for '{reference}'.";
        }

        var sb = new StringBuilder();
        foreach (var verse in rows.Take(MaxVerses))
        {
            sb.AppendLine($"{GermanReferenceFormatter.Format(verse.HebRefId)}  {verse.VerseText}");
            if (includeLemmas)
            {
                sb.AppendLine($"  Lemmas: {verse.LemmaText}");
            }
        }

        if (rows.Count > MaxVerses)
        {
            sb.AppendLine($"(Truncated: showing the first {MaxVerses} verses. Request a smaller range for the rest.)");
        }

        return sb.ToString().TrimEnd();
    }
}
