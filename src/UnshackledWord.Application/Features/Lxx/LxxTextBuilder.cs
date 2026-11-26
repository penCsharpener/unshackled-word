using UnshackledWord.Application.Features.Seed.Anchoring;
using UnshackledWord.Domain.Extensions;
using UnshackledWord.Domain.Models.Dbo.Lxx;

namespace UnshackledWord.Application.Features.Lxx;

/// <summary>One verse row with its words, before insert (<see cref="LxxWordDbo.LxxVerseId"/> is set by the repository).</summary>
public sealed record LxxVerseText(LxxVerseDbo Verse, IReadOnlyList<LxxWordDbo> Words);

/// <summary>The rows of <c>LxxVerses</c>/<c>LxxWords</c> built from a versification result, plus what was left out.</summary>
public sealed record LxxText(IReadOnlyList<LxxVerseText> Verses, int SkippedEmpty, int SkippedNoWords, IReadOnlyDictionary<LxxDroppedToken, int> Dropped)
{
    public int WordCount => Verses.Sum(v => v.Words.Count);
}

/// <summary>
/// Maps the units of <see cref="LxxVersification.Map"/> to rows of <c>LxxVerses</c>/<c>LxxWords</c> (roadmap 0028.3):
/// <list type="bullet">
/// <item>empty Swete verses (<see cref="LxxVerseMapping.IsEmpty"/>, <c>absent</c> by rule) and units without a word after
/// <see cref="LxxWordFilter"/> are skipped;</item>
/// <item><c>SortOrder</c> is the unit's <see cref="LxxVerseMapping.Order"/> (Swete's reading order);</item>
/// <item><c>absent</c> units that have text (Swete text without Rahlfs counterpart; none in the pinned data) are kept with
/// <c>RahlfsRefId</c> NULL and Swete's chapter and verse, so no Greek is lost;</item>
/// <item>words are numbered 1..n after filtering, <c>GreekNoDiacritics</c> as for STEP's Greek words (plus Swete's square brackets for
/// supplied text removed, Gen 1:20-2:3 and two more words), <c>SrcHash</c> = <see cref="SourceKey.Hash"/>.</item>
/// </list>
/// </summary>
public static class LxxTextBuilder
{
    public static LxxText Build(IEnumerable<LxxVerseMapping> mappings)
    {
        var verses = new List<LxxVerseText>();
        var dropped = new Dictionary<LxxDroppedToken, int>();
        var skippedEmpty = 0;
        var skippedNoWords = 0;

        foreach (var m in mappings)
        {
            if (m.IsEmpty)
            {
                skippedEmpty++;
                continue;
            }

            var greek = LxxWordFilter.Filter(m.Words.Select(w => w.Text), m.RahlfsBook, dropped);
            if (greek.Count == 0)
            {
                skippedNoWords++;
                continue;
            }

            var unmapped = m.RahlfsRefId == 0;
            var verse = new LxxVerseDbo
            {
                BibleBookId = m.BibleBookId,
                TextForm = m.TextForm,
                Chapter = unmapped ? m.SweteChapter : m.Chapter,
                Verse = unmapped ? m.SweteVerse : m.Verse,
                Subverse = unmapped ? 0 : m.Subverse,
                RahlfsRefId = unmapped ? null : m.RahlfsRefId,
                SortOrder = m.Order,
                SweteRef = m.SweteRef,
                LxxRefId = m.LxxRefId,
                HebRefId = m.HebRefId,
                AfterHebRefId = m.AfterHebRefId,
                MapKind = m.MapKind,
            };

            var words = greek.Select((g, i) => new LxxWordDbo
            {
                Position = i + 1,
                Greek = g,
                GreekNoDiacritics = PlainForm(g),
                SrcHash = SourceKey.Hash(g),
            }).ToList();

            verses.Add(new LxxVerseText(verse, words));
        }

        return new LxxText(verses, skippedEmpty, skippedNoWords, dropped);
    }

    /// <summary><see cref="StringExtensions.RemoveGreekDiacritics"/> without Swete's brackets for supplied text (<c>[τοῖ]ς</c>).</summary>
    public static string PlainForm(string greek) => greek.RemoveGreekDiacritics()!.Replace("[", "").Replace("]", "");
}
