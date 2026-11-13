using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace UnshackledWord.Application.Features.Seed.Mappings;

/// <summary>
/// The mapping edges of one German verse in the form the content hash sees them: no database id, no text of the words, only positions
/// (0 where a value is absent), so the hash of a database verse and the hash of the seed's rows for the same verse are comparable.
/// </summary>
public readonly record struct VerseEdge(int Pos, string Part, int SrcRef, int SrcPos, bool Added, int ParentRef, int ParentPos);

/// <summary>
/// Content hash of a verse's mapping edges (roadmap 0010.10). The same formula is computed in SQL (<c>SeedVerseState</c>): lines
/// <c>Pos|Part|SrcRef|SrcPos|true|ParentRef|ParentPos</c> ordered by position, source verse and source position, joined by LF, SHA-256
/// over the UTF-8 bytes, lower-case hex. Changes when anyone adds, removes or re-links an edge of the verse.
/// </summary>
public static class MappingVerseHash
{
    public static string Compute(IEnumerable<VerseEdge> edges)
    {
        var lines = edges.Select(Line).ToList();
        lines.Sort(StringComparer.Ordinal);

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', lines))));
    }

    private static string Line(VerseEdge e)
    {
        var added = e.Added ? "true" : "false";

        return string.Create(CultureInfo.InvariantCulture, $"{e.Pos}|{e.Part}|{e.SrcRef}|{e.SrcPos}|{added}|{e.ParentRef}|{e.ParentPos}");
    }
}
