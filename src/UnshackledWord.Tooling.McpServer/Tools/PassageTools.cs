using System.ComponentModel;
using System.Text;
using ModelContextProtocol.Server;
using UnshackledWord.Application.Repositories;
using UnshackledWord.Domain.Models.BibleStructure;

namespace UnshackledWord.Tooling.McpServer.Tools;

[McpServerToolType]
public static class PassageTools
{
    public const int MaxVerses = 200;

    [McpServerTool(Name = "get_passage", ReadOnly = true)]
    [Description("Returns the plain German Elberfelder 1871 text of a Bible passage, one line per verse.")]
    public static async Task<string> GetPassage(
        IElb1871VerseRepository verses,
        [Description("Bible reference in German or English notation, e.g. 'Joh 3,16-18', 'John 3:16', 'Röm 8', '1Mo 1,1-2,3'.")] string reference,
        CancellationToken cancellationToken)
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
            var r = BibleReference.FromRefId(verse.HebRefId);
            sb.AppendLine($"{GermanBookNames.GetAbbreviation(r.BookId)} {r.Chapter},{r.Verse}  {verse.VerseText}");
        }

        if (rows.Count > MaxVerses)
        {
            sb.AppendLine($"(Truncated: showing the first {MaxVerses} verses. Request a smaller range for the rest.)");
        }

        return sb.ToString().TrimEnd();
    }
}
