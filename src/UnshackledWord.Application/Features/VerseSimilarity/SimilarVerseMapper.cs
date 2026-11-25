using UnshackledWord.Domain.Models.BibleStructure;
using UnshackledWord.Domain.WebApi.Elberfelder.GetSimilarVerses;

namespace UnshackledWord.Application.Features.VerseSimilarity;

/// <summary>Maps a stored hit and its read-time explanation to the API shape (roadmap 0027.3).</summary>
public static class SimilarVerseMapper
{
    /// <summary>Hebrew/Aramaic books are 1–39; the STEP key has the same <c>book * 1_000_000</c> form as the Elberfelder one.</summary>
    public static Testament TestamentOf(int refId) => refId / 1_000_000 <= 39 ? Testament.Old : Testament.New;

    /// <summary>The explanation's Strong's numbers (shared and in phrases), for one label lookup.</summary>
    public static IEnumerable<string> StrongsOf(VerseSimilarityExplanation? explanation) =>
        explanation is null
            ? []
            : explanation.Shared.Select(s => s.Strongs).Concat(explanation.Phrases.SelectMany(p => p));

    /// <summary>The words of one verse grouped by Strong's, built once per request from <see cref="ElbWordStrongs"/> rows.</summary>
    public static IReadOnlyDictionary<int, ILookup<string, int>> GroupWordStrongs(IEnumerable<ElbWordStrongs> rows) =>
        rows
            .GroupBy(r => r.HebRefId)
            .ToDictionary(g => g.Key, g => g.ToLookup(r => r.Strongs, r => r.PositionInVerse, StringComparer.Ordinal));

    /// <summary>Ascending distinct positions of the verse's words that render any of <paramref name="strongs"/>; empty when the verse is unknown.</summary>
    public static List<int> PositionsOf(IReadOnlyDictionary<int, ILookup<string, int>> wordStrongs, int hebRefId, IEnumerable<string> strongs) =>
        wordStrongs.TryGetValue(hebRefId, out var verse)
            ? strongs.Distinct(StringComparer.Ordinal).SelectMany(s => verse[s]).Distinct().Order().ToList()
            : [];

    /// <param name="explanation">Null when a verse is not in the corpus: the lists stay empty, the stored counts stay.</param>
    public static SimilarVerseHitResponse ToHit(SimilarHitRow row, int targetHebRefId, VerseSimilarityExplanation? explanation, IReadOnlyDictionary<string, StrongsLabel> labels,
        IReadOnlyDictionary<int, ILookup<string, int>>? wordStrongs = null)
    {
        var target = BibleReference.FromRefId(targetHebRefId);

        string Lemma(string strongs) => labels.TryGetValue(strongs, out var label) && label.Lemma.Length > 0 ? label.Lemma : strongs;

        var sharedStrongs = explanation?.Shared.Select(x => x.Strongs).ToList() ?? [];

        return new SimilarVerseHitResponse
        {
            HebRefId = targetHebRefId,
            BibleBookId = target.BookId,
            Chapter = target.Chapter,
            Verse = target.Verse,
            Text = row.TargetText ?? string.Empty,
            Score = (double)row.Score,
            Rank = row.Rank,
            SharedCount = row.SharedCount,
            PhraseCount = row.PhraseCount,

            // Explain already orders rarest first (idf descending, then Strong's ordinal).
            Shared = explanation?.Shared
                .Select(s =>
                {
                    labels.TryGetValue(s.Strongs, out var label);
                    return new SimilarSharedStrongsResponse
                    {
                        Strongs = s.Strongs,
                        Lemma = label?.Lemma ?? string.Empty,
                        Transliteration = label?.Transliteration ?? string.Empty,
                        Gloss = label?.Gloss ?? string.Empty,
                    };
                })
                .ToList() ?? [],
            Phrases = explanation?.Phrases
                .Select(p => new SimilarPhraseResponse { Text = string.Join(' ', p.Select(Lemma)), Strongs = p.ToList() })
                .ToList() ?? [],
            TargetPositions = wordStrongs is null ? [] : PositionsOf(wordStrongs, targetHebRefId, sharedStrongs),
            SourcePositions = wordStrongs is null ? [] : PositionsOf(wordStrongs, row.SourceHebRefId, sharedStrongs),
        };
    }
}
