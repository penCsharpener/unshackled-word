using System.ComponentModel;
using System.Text;
using ModelContextProtocol.Server;
using UnshackledWord.Application.Repositories;
using UnshackledWord.Domain.Models.BibleStructure;

namespace UnshackledWord.Tooling.McpServer.Tools;

[McpServerToolType]
public static class CrossReferenceTools
{
    public const int DefaultLimit = 50;
    public const int MaxLimit = 200;
    private const int MaxTextVersesPerTarget = 3;

    [McpServerTool(Name = "get_cross_references", ReadOnly = true)]
    [Description("Returns Treasury of Scripture Knowledge (TSK) cross-references for a verse or a range of verses, grouped by source verse and TSK keyword. Optionally includes the Elberfelder 1871 text of the targets.")]
    public static async Task<string> GetCrossReferences(
        ITskRepository tsk,
        IElb1871VerseRepository verses,
        [Description("Bible reference in German or English notation, e.g. 'Joh 3,16', 'Röm 8,1-4', 'Ps 23'.")] string reference,
        [Description("Include the German text of each target (long target ranges show only their first 3 verses). Default false.")] bool includeText = false,
        [Description("Maximum number of cross-references to return (default 50, max 200).")] int limit = DefaultLimit,
        CancellationToken cancellationToken = default)
    {
        var parsed = BibleReferenceParser.Parse(reference);
        if (parsed.Range is null)
        {
            return parsed.Error!;
        }

        limit = Math.Clamp(limit, 1, MaxLimit);
        var range = parsed.Range;
        var rows = await tsk.GetOutgoingAsync(range.StartRefId, range.EndRefId, limit + 1, cancellationToken);
        if (rows.Count == 0)
        {
            return $"No cross-references found for '{reference}'.";
        }

        var truncated = rows.Count > limit;
        var shown = rows.Take(limit).ToList();

        var sb = new StringBuilder();
        sb.AppendLine($"Cross-references (TSK) for {reference.Trim()}");
        foreach (var source in shown.GroupBy(r => r.SourceHebRefId))
        {
            foreach (var scope in source.GroupBy(r => r.Scope))
            {
                sb.AppendLine($"[{GermanReferenceFormatter.Format(source.Key)}] {scope.Key}");
                foreach (var target in scope)
                {
                    var label = GermanReferenceFormatter.Format(target.TargetStartHebRefId, target.TargetEndHebRefId);
                    if (!includeText)
                    {
                        sb.AppendLine($"  - {label}");
                        continue;
                    }

                    var textEnd = Math.Min(target.TargetEndHebRefId ?? target.TargetStartHebRefId,
                        target.TargetStartHebRefId + MaxTextVersesPerTarget - 1);
                    var text = await verses.GetByHebRefIdRangeAsync(target.TargetStartHebRefId, textEnd, MaxTextVersesPerTarget, cancellationToken);
                    sb.AppendLine($"  - {label}: {string.Join(" ", text.Select(v => v.VerseText))}");
                }
            }
        }

        if (truncated)
        {
            sb.AppendLine($"(Truncated: showing the first {limit} cross-references. Request a smaller range or a higher limit.)");
        }

        return sb.ToString().TrimEnd();
    }
}
