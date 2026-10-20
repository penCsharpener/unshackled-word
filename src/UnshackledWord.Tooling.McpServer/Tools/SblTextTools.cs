using System.ComponentModel;
using System.Text;
using ModelContextProtocol.Server;
using UnshackledWord.Application.Repositories;
using UnshackledWord.Domain.Models.BibleStructure;

namespace UnshackledWord.Tooling.McpServer.Tools;

[McpServerToolType]
public static class SblTextTools
{
    public const int MaxVerses = 200;

    [McpServerTool(Name = "get_sbl_text", ReadOnly = true)]
    [Description("Returns the Greek New Testament text (SBL edition) of a passage, one line per verse. New Testament only.")]
    public static async Task<string> GetSblText(
        ISblTextRepository sbl,
        [Description("New Testament reference in German or English notation, e.g. 'Joh 3,16-18', 'Röm 8', 'Matt 5:3'.")] string reference,
        CancellationToken cancellationToken)
    {
        var parsed = BibleReferenceParser.Parse(reference);
        if (parsed.Range is null)
        {
            return parsed.Error!;
        }

        var range = parsed.Range;
        if (range.BookId < 40 || range.BookId > 66)
        {
            return $"The SBL text covers the New Testament only; '{reference}' is not in the New Testament (books Mt–Offb).";
        }

        // NT verse numbering is identical in Elberfelder and SBL, so the parsed ids can be used directly.
        var rows = await sbl.GetByRefIdRangeAsync(range.StartRefId, range.EndRefId, MaxVerses + 1, cancellationToken);
        if (rows.Count == 0)
        {
            return $"No verses found for '{reference}'.";
        }

        var sb = new StringBuilder();
        foreach (var verse in rows.Take(MaxVerses))
        {
            sb.AppendLine($"{GermanReferenceFormatter.Format(verse.LxxRefId)}  {verse.VerseText}");
        }

        if (rows.Count > MaxVerses)
        {
            sb.AppendLine($"(Truncated: showing the first {MaxVerses} verses. Request a smaller range for the rest.)");
        }

        return sb.ToString().TrimEnd();
    }
}
