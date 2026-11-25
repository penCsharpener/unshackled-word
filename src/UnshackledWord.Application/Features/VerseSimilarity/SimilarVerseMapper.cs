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

    /// <param name="explanation">Null when a verse is not in the corpus: the lists stay empty, the stored counts stay.</param>
    public static SimilarVerseHitResponse ToHit(SimilarHitRow row, int targetHebRefId, VerseSimilarityExplanation? explanation, IReadOnlyDictionary<string, StrongsLabel> labels)
    {
        var target = BibleReference.FromRefId(targetHebRefId);

        string Lemma(string strongs) => labels.TryGetValue(strongs, out var label) && label.Lemma.Length > 0 ? label.Lemma : strongs;

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
        };
    }
}
