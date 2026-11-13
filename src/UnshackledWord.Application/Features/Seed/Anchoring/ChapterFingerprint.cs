using System.Security.Cryptography;
using System.Text;

namespace UnshackledWord.Application.Features.Seed.Anchoring;

/// <summary>
/// Short hash of a chapter's token list. Equal fingerprints mean that every token has the same reference, position and text, so an
/// importer can take the seed's keys as they are and skip the alignment.
/// </summary>
public static class ChapterFingerprint
{
    public const int HexLength = 12;

    /// <summary>
    /// SHA-256 over the lines <c>Ref|Pos|Text</c> (LF separated, UTF-8) of the chapter's tokens in the given order, first 12 hex digits,
    /// lower case. The reference and position are part of the hash on purpose: the same words split into verses differently must
    /// not look equal.
    /// </summary>
    public static string Compute(IEnumerable<AnchorToken> tokens)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        foreach (var token in tokens)
        {
            hash.AppendData(Encoding.UTF8.GetBytes($"{token.Ref}|{token.Pos}|{token.Text}\n"));
        }

        return Convert.ToHexStringLower(hash.GetHashAndReset()).Substring(0, HexLength);
    }
}
