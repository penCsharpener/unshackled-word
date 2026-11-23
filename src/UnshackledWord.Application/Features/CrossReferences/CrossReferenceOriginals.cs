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
}
