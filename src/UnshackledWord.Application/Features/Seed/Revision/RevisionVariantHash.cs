using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace UnshackledWord.Application.Features.Seed.Revision;

/// <summary>
/// Content hash of one variant (roadmap 0011.12), computed from the seed lines the variant exports as: text, note, base level and hash,
/// review flag and every mapping line. The database side is hashed from the same lines (read back with the export queries), so "the
/// database still equals the last import" is a plain comparison and any editor save (text or mapping) changes the hash.
/// </summary>
public static class RevisionVariantHash
{
    private const char Separator = '\u001f';

    public static string Compute(RevisionVerseSeedLine verse, IEnumerable<RevisionMappingSeedLine> mapping)
    {
        var lines = mapping.Select(Line).ToList();
        lines.Sort(StringComparer.Ordinal);

        var head = string.Join(Separator, verse.Text, verse.Note ?? string.Empty, verse.BasedOnLevel?.ToString(CultureInfo.InvariantCulture) ?? string.Empty, verse.BaseHash, verse.Reviewed ? "1" : "0");
        var text = head + "\n" + string.Join('\n', lines);

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    }

    private static string Line(RevisionMappingSeedLine l)
    {
        return string.Join(
            Separator,
            l.Pos.ToString(CultureInfo.InvariantCulture),
            l.Form,
            l.Part,
            l.SrcRef?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            l.SrcPos?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            l.SrcHash,
            l.Added ? "1" : "0",
            l.ParentPos?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            l.ParentForm,
            l.Origin);
    }
}
