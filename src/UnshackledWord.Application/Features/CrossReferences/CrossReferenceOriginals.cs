using System.Text.RegularExpressions;
using UnshackledWord.Domain.Models.Study;
using UnshackledWord.Domain.WebApi.Elberfelder.GetVersesOfChapter;

namespace UnshackledWord.Application.Features.CrossReferences;

/// <summary>Builds the plain original-language text of verses for the cross reference hover card (roadmap 0024.1).</summary>
public static class CrossReferenceOriginals
{
    public const int LastHebrewBookId = 39;

    public static bool IsHebrewBook(int hebRefId) => hebRefId / 1_000_000 <= LastHebrewBookId;

    /// <summary>
    /// One entry per verse that has words. Greek is "greek". OT verses are "hebrew", or "aramaic" when more than half of the
    /// verse's words have an Aramaic grammar code (STEP codes start with <c>A</c>, Hebrew ones with <c>H</c>).
    /// </summary>
    public static Dictionary<int, VerseOriginalTextResponse> Build(IEnumerable<ChapterOriginalWordRow> words, bool hebrew)
    {
        return words
            .GroupBy(w => w.HebRefId)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var ordered = g.OrderBy(w => w.Position).ThenBy(w => w.Id).ToList();
                    var aramaic = hebrew && ordered.Count(w => w.MorphologyCode?.TrimStart().StartsWith('A') == true) * 2 > ordered.Count;
                    return new VerseOriginalTextResponse
                    {
                        Language = !hebrew ? "greek" : aramaic ? "aramaic" : "hebrew",
                        Text = string.Join(' ', ordered.Select(w => w.Original.Trim()).Where(t => t.Length > 0)),
                    };
                });
    }

    /// <summary>The base number of a Strong's key ("G3004G" and "H1254A" become "G3004" and "H1254"), null when there is none.</summary>
    public static string? BaseNumber(string? strongs)
    {
        if (string.IsNullOrEmpty(strongs))
        {
            return null;
        }

        var match = BaseNumberPattern.Match(strongs);
        return match.Success ? match.Value : null;
    }

    private static readonly Regex BaseNumberPattern = new(@"[GH]\d+", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Distinct Strong's keys per verse in order of first position. Words without a Strong's number and keys whose base number is
    /// in <paramref name="frequentBases"/> (see <see cref="BaseNumber"/>) are left out; a verse without keys has no entry.
    /// </summary>
    public static Dictionary<int, List<string>> BuildStrongs(IEnumerable<ChapterOriginalWordRow> words, IReadOnlySet<string> frequentBases)
    {
        var result = new Dictionary<int, List<string>>();
        foreach (var group in words.GroupBy(w => w.HebRefId))
        {
            var keys = group
                .OrderBy(w => w.Position)
                .ThenBy(w => w.Id)
                .Select(w => w.Strongs?.Trim())
                .Where(k => !string.IsNullOrEmpty(k))
                .Select(k => k!)
                .Where(k => BaseNumber(k) is not { } b || !frequentBases.Contains(b))
                .Distinct()
                .ToList();
            if (keys.Count > 0)
            {
                result[group.Key] = keys;
            }
        }

        return result;
    }
}
