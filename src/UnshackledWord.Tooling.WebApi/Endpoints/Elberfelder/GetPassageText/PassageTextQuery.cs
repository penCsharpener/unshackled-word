using UnshackledWord.Application.Features.Revision;
using UnshackledWord.Application.Repositories;
using UnshackledWord.Domain.Models.BibleStructure;
using UnshackledWord.Domain.Models.Study;
using UnshackledWord.Domain.WebApi.Elberfelder.GetPassageText;
using UnshackledWord.Domain.WebApi.Elberfelder.GetVerseCrossReferenceTexts;
using UnshackledWord.Domain.WebApi.Elberfelder.GetVersesOfChapter;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Elberfelder.GetPassageText;

public static class PassageTextQuery
{
    private static readonly Dictionary<int, BibleBook> Books = BibleBook.AllBooks;

    /// <summary>
    /// Reads the texts of the spans at the reader level (same level rule as the cross reference texts).
    /// Spans beyond <see cref="PassageSpanParser.MaxSpans"/> and verses beyond <see cref="PassageSpanParser.MaxVerses"/> are left out and set <see cref="GetPassageTextResponse.Truncated"/>.
    /// </summary>
    public static async Task<GetPassageTextResponse> ReadAsync(IElb1871VerseRepository verses, string spans, int level, CancellationToken ct = default)
    {
        if (!PassageSpanParser.TryParse(spans, out var parsed))
        {
            throw new ArgumentException("Spans must be verse ids or id ranges of existing verses, e.g. 66002001-66002005,66002007.", nameof(spans));
        }

        var response = new GetPassageTextResponse
        {
            Truncated = parsed.Count > PassageSpanParser.MaxSpans,
        };
        var remaining = PassageSpanParser.MaxVerses;

        foreach (var span in parsed.Take(PassageSpanParser.MaxSpans))
        {
            foreach (var (first, last) in ChapterRuns(span))
            {
                if (remaining == 0)
                {
                    // Every run holds at least one verse, so a run left over means verses were cut.
                    response.Truncated = true;
                    return response;
                }

                var end = last;
                if (last - first + 1 > remaining)
                {
                    end = first + remaining - 1;
                    response.Truncated = true;
                }

                var count = end - first + 1;
                remaining -= count;
                response.Passages.Add(await ReadRunAsync(verses, first, end, count, level, ct));
            }
        }

        return response;
    }

    /// <summary>Splits a span into runs of verse ids that stay within one chapter; lazy, so a huge span costs nothing beyond the verses taken.</summary>
    public static IEnumerable<(int First, int Last)> ChapterRuns(PassageSpan span)
    {
        var (bookId, startChapter, startVerse) = PassageSpanParser.Decompose(span.StartRefId);
        var (_, endChapter, endVerse) = PassageSpanParser.Decompose(span.EndRefId);
        var verseCounts = Books[bookId].VersesPerChapterHeb;

        for (var chapter = startChapter; chapter <= endChapter; chapter++)
        {
            var first = chapter == startChapter ? startVerse : 1;
            var last = chapter == endChapter ? endVerse : verseCounts[chapter - 1];
            yield return (PassageSpanParser.ToRefId(bookId, chapter, first), PassageSpanParser.ToRefId(bookId, chapter, last));
        }
    }

    private static async Task<CrossReferenceTextResponse> ReadRunAsync(IElb1871VerseRepository verses, int first, int last, int count, int level, CancellationToken ct)
    {
        var (bookId, chapter, verse) = PassageSpanParser.Decompose(first);
        var verseRows = level == RevisionSql.BaseOnlyLevel
            ? (await verses.GetByHebRefIdRangeAsync(first, last, count, ct))
                .Select(r => new ChapterVerseResponse { Verse = r.HebRefId % 1000, Text = r.VerseText })
                .ToList()
            : (await verses.GetEffectiveByHebRefIdRangeAsync(first, last, level, ct))
                .Select(r => new ChapterVerseResponse { Verse = r.HebRefId % 1000, Text = r.VerseText })
                .ToList();

        return new CrossReferenceTextResponse
        {
            BibleBookId = bookId,
            Chapter = chapter,
            Verse = verse,
            EndVerse = count == 1 ? null : last % 1000,
            Verses = verseRows,
        };
    }
}
