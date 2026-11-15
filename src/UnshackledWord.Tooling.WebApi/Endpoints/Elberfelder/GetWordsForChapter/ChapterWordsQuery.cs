using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Features.Revision;
using UnshackledWord.Domain.Models.Dbo;
using UnshackledWord.Domain.WebApi.BibleTagger.Reading;
using UnshackledWord.Domain.WebApi.Elberfelder;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Elberfelder.GetWordsForChapter;

/// <summary>A word row of a chapter: an Elb1871 word or, when RevisionWordId is set, a word of a revision variant.</summary>
public sealed class ChapterWordRow : Elb1871WordDbo
{
    public int? RevisionWordId { get; set; }
    public int? RevisionVerseId { get; set; }
    public int? RevisionLevel { get; set; }
}

/// <summary>Reads the words of one chapter at a literalness level (roadmap 0011). Level 0 (Elb1871 only) runs the untouched base query.</summary>
public static class ChapterWordsQuery
{
    public const int BaseLevel = RevisionSql.BaseOnlyLevel;

    public static async Task<(List<WordResponse> Words, List<RevisedVerseResponse>? RevisedVerses)> ReadAsync(
        IDbReader reader, int bibleBookId, int chapter, int level)
    {
        var hebrew = bibleBookId is >= 1 and <= 39;
        var (from, to) = RevisionSql.ChapterRange(bibleBookId, chapter);
        var rows = level == BaseLevel
            ? await reader.ReadAsListAsync<ChapterWordRow>(hebrew ? BaseHebrew : BaseGreek, new { BibleBookId = bibleBookId, Chapter = chapter })
            : await reader.ReadAsListAsync<ChapterWordRow>(hebrew ? RevisedHebrew : RevisedGreek, new { Level = level, FromRefId = from, ToRefId = to });

        var words = rows.Select(x => new WordResponse
        {
            Id = x.Id,
            Lemma = x.Lemma,
            PlainWord = x.PlainWord,
            PositionInVerse = x.PositionInVerse,
            Strongs = x.Strongs,
            Verse = x.Verse,
            WordInContext = x.WordInContext,
            Original = x.Original,
            GrammaticalKey = x.GrammaticalKey,
            RevisionWordId = x.RevisionWordId
        }).ToList();

        var revised = rows
            .Where(x => x.RevisionVerseId.HasValue)
            .GroupBy(x => x.RevisionVerseId!.Value)
            .Select(g => new RevisedVerseResponse { Verse = g.First().Verse, Level = g.First().RevisionLevel ?? 0, RevisionVerseId = g.Key })
            .OrderBy(v => v.Verse)
            .ToList();

        return (words, revised.Count == 0 ? null : revised);
    }

    private const string BaseHebrew = """
                                      SELECT ew."Id"
                                           , ew."Verse"
                                           , ew."PositionInVerse"
                                           , ew."WordInContext"
                                           , ew."PlainWord"
                                           , shw."RootDisambiguatedStrongsInstance" "Strongs"
                                           , shw."Hebrew" "Original"
                                           , shw."Grammar" "GrammaticalKey"
                                      FROM "unshackled-word"."Elb1871Words" ew
                                          LEFT JOIN "unshackled-word"."Elb1871HebrewMapping" ehm ON ew."Id" = ehm."ElbWordId"
                                          LEFT JOIN "unshackled-word"."StepHebrewWords" shw ON ehm."StepWordId" = shw."Id"
                                      WHERE 1=1
                                          AND ew."BibleBookId" = @BibleBookId
                                          AND ew."Chapter" = @Chapter
                                      ORDER BY ew."BibleBookId", ew."Chapter", ew."Verse", ew."PositionInVerse"
                                      """;

    private const string BaseGreek = """
                                     SELECT ew."Id"
                                          , ew."Verse"
                                          , ew."PositionInVerse"
                                          , ew."WordInContext"
                                          , ew."PlainWord"
                                          , sgw."DisambiguatedStrongs" "Strongs"
                                          , sgw."Lemma"
                                          , sgw."Greek" "Original"
                                          , sgw."Morphology" "GrammaticalKey"
                                     FROM "unshackled-word"."Elb1871Words" ew
                                         LEFT JOIN "unshackled-word"."Elb1871GreekMapping" egm ON ew."Id" = egm."ElbWordId"
                                         LEFT JOIN "unshackled-word"."StepGreekWords" sgw ON egm."StepWordId" = sgw."Id"
                                     WHERE 1=1
                                         AND ew."BibleBookId" = @BibleBookId
                                         AND ew."Chapter" = @Chapter
                                     ORDER BY ew."BibleBookId", ew."Chapter", ew."Verse", ew."PositionInVerse"
                                     """;

    private const string RevisedHebrew = $"""
                                          WITH ev AS ({RevisionSql.EffectiveVerses})
                                          SELECT ew."Id"
                                               , ew."Verse"
                                               , ew."PositionInVerse"
                                               , ew."WordInContext"
                                               , ew."PlainWord"
                                               , shw."RootDisambiguatedStrongsInstance" "Strongs"
                                               , shw."Hebrew" "Original"
                                               , shw."Grammar" "GrammaticalKey"
                                               , NULL::int4 "RevisionWordId"
                                               , NULL::int4 "RevisionVerseId"
                                               , NULL::int4 "RevisionLevel"
                                               , ew."HebRefId" "SortRefId"
                                          FROM ev
                                              JOIN "unshackled-word"."Elb1871Words" ew ON ew."HebRefId" = ev."HebRefId"
                                              LEFT JOIN "unshackled-word"."Elb1871HebrewMapping" ehm ON ew."Id" = ehm."ElbWordId"
                                              LEFT JOIN "unshackled-word"."StepHebrewWords" shw ON ehm."StepWordId" = shw."Id"
                                          WHERE ev."RevisionVerseId" IS NULL
                                          UNION ALL
                                          SELECT COALESCE(bw."Id", 0)
                                               , ev."HebRefId" % 1000
                                               , rw."PositionInVerse"
                                               , rw."WordInContext"
                                               , rw."PlainWord"
                                               , shw."RootDisambiguatedStrongsInstance"
                                               , shw."Hebrew"
                                               , shw."Grammar"
                                               , rw."Id"
                                               , ev."RevisionVerseId"
                                               , ev."Level"
                                               , ev."HebRefId"
                                          FROM ev
                                              JOIN "unshackled-word"."RevisionWords" rw ON rw."RevisionVerseId" = ev."RevisionVerseId"
                                              LEFT JOIN "unshackled-word"."Elb1871Words" bw ON bw."HebRefId" = ev."HebRefId" AND bw."PositionInVerse" = rw."BasePositionInVerse"
                                              LEFT JOIN "unshackled-word"."RevisionMapping" rm ON rm."RevisionWordId" = rw."Id" AND rm."Lang" = 'H'
                                              LEFT JOIN "unshackled-word"."StepHebrewWords" shw ON rm."StepWordId" = shw."Id"
                                          ORDER BY "SortRefId", "PositionInVerse"
                                          """;

    private const string RevisedGreek = $"""
                                         WITH ev AS ({RevisionSql.EffectiveVerses})
                                         SELECT ew."Id"
                                              , ew."Verse"
                                              , ew."PositionInVerse"
                                              , ew."WordInContext"
                                              , ew."PlainWord"
                                              , sgw."DisambiguatedStrongs" "Strongs"
                                              , sgw."Lemma"
                                              , sgw."Greek" "Original"
                                              , sgw."Morphology" "GrammaticalKey"
                                              , NULL::int4 "RevisionWordId"
                                              , NULL::int4 "RevisionVerseId"
                                              , NULL::int4 "RevisionLevel"
                                              , ew."HebRefId" "SortRefId"
                                         FROM ev
                                             JOIN "unshackled-word"."Elb1871Words" ew ON ew."HebRefId" = ev."HebRefId"
                                             LEFT JOIN "unshackled-word"."Elb1871GreekMapping" egm ON ew."Id" = egm."ElbWordId"
                                             LEFT JOIN "unshackled-word"."StepGreekWords" sgw ON egm."StepWordId" = sgw."Id"
                                         WHERE ev."RevisionVerseId" IS NULL
                                         UNION ALL
                                         SELECT COALESCE(bw."Id", 0)
                                              , ev."HebRefId" % 1000
                                              , rw."PositionInVerse"
                                              , rw."WordInContext"
                                              , rw."PlainWord"
                                              , sgw."DisambiguatedStrongs"
                                              , sgw."Lemma"
                                              , sgw."Greek"
                                              , sgw."Morphology"
                                              , rw."Id"
                                              , ev."RevisionVerseId"
                                              , ev."Level"
                                              , ev."HebRefId"
                                         FROM ev
                                             JOIN "unshackled-word"."RevisionWords" rw ON rw."RevisionVerseId" = ev."RevisionVerseId"
                                             LEFT JOIN "unshackled-word"."Elb1871Words" bw ON bw."HebRefId" = ev."HebRefId" AND bw."PositionInVerse" = rw."BasePositionInVerse"
                                             LEFT JOIN "unshackled-word"."RevisionMapping" rm ON rm."RevisionWordId" = rw."Id" AND rm."Lang" = 'G'
                                             LEFT JOIN "unshackled-word"."StepGreekWords" sgw ON rm."StepWordId" = sgw."Id"
                                         ORDER BY "SortRefId", "PositionInVerse"
                                         """;
}
