using System.Security.Cryptography;
using System.Text;

namespace UnshackledWord.Infrastructure.Seed;

public static class SeedVersion
{
    /// <summary>
    /// SHA-256 (64 hex) over the lines <c>path TAB sha256</c> of all seed files of a kind in manifest order. Changes exactly when file
    /// content changes; the export time is not part of it, so an unchanged export keeps its version.
    /// </summary>
    public static string Compute(IEnumerable<SeedFileEntry> files)
    {
        var text = string.Concat(files.Select(f => $"{f.Path}\t{f.Sha256}\n"));

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    }
}
