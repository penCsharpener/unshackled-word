namespace UnshackledWord.Application.Features.Revision;

/// <summary>SQL fragments shared by every reader of the Bible revision (roadmap 0011 "Effective variant").</summary>
public static class RevisionSql
{
    /// <summary>The level of the unrevised Elb1871 (a revision flagged IsBase); it is never stored as a variant. A revision with a variant may share this level and wins over the base there.</summary>
    public const int BaseLevel = 200;

    /// <summary>Pseudo level "only the unrevised Elb1871 text, no variant at all"; also the default <c>BasedOnLevel</c> of a save.</summary>
    public const int BaseOnlyLevel = 0;

    /// <summary>
    /// Per verse of the range the variant the slider value shows: start at the base level (Elb1871), walk towards @Level and take the last
    /// variant passed. RevisionVerseId IS NULL = the base text. Parameters: @Level, @FromRefId, @ToRefId. Use as a CTE body.
    /// </summary>
    public const string EffectiveVerses = """
                                          SELECT v."HebRefId"
                                               , r."Id"    AS "RevisionVerseId"
                                               , r."Level" AS "Level"
                                          FROM "unshackled-word"."Elb1871Verses" v
                                              LEFT JOIN LATERAL (SELECT rv."Id"
                                                                      , rev."Level"
                                                                 FROM "unshackled-word"."RevisionVerses" rv
                                                                     JOIN "unshackled-word"."Revisions" rev ON rev."Id" = rv."RevisionId"
                                                                 WHERE rv."HebRefId" = v."HebRefId"
                                                                     AND rv."IsActive"
                                                                     AND rev."Level" BETWEEN least(@Level, 200) AND greatest(@Level, 200)
                                                                 ORDER BY abs(rev."Level" - 200) DESC
                                                                 LIMIT 1) r ON TRUE
                                          WHERE v."HebRefId" BETWEEN @FromRefId AND @ToRefId
                                          """;

    /// <summary>Books 1-39 are Hebrew (and Aramaic), 40-66 Greek; the single source of <c>RevisionMapping."Lang"</c>.</summary>
    public static bool IsHebrew(int bibleBookId) => bibleBookId <= 39;

    public static string Lang(int bibleBookId) => IsHebrew(bibleBookId) ? "H" : "G";

    /// <summary>Book, chapter and verse of a HebRefId (BBCCCVVV).</summary>
    public static (int BibleBookId, int Chapter, int Verse) Split(int hebRefId) => (hebRefId / 1_000_000, hebRefId / 1_000 % 1_000, hebRefId % 1_000);

    /// <summary>HebRefId (BBCCCVVV) of one verse.</summary>
    public static int HebRefId(int bibleBookId, int chapter, int verse) => bibleBookId * 1_000_000 + chapter * 1_000 + verse;

    /// <summary>HebRefId range of a chapter (BBCCCVVV): verse 0 (kept like the pre-revision reads, in case a title verse exists) to verse 999.</summary>
    public static (int From, int To) ChapterRange(int bibleBookId, int chapter)
    {
        var start = bibleBookId * 1_000_000 + chapter * 1_000;
        return (start, start + 999);
    }
}
