using System.Data;
using Dapper;

namespace UnshackledWord.Infrastructure.Repositories;

/// <summary>Values of <c>RevisionVerseAudit."Action"</c>.</summary>
public static class RevisionAuditAction
{
    public const string Text = "text";
    public const string Mapping = "mapping";
    public const string Withdraw = "withdraw";
}

/// <summary>
/// Appends one <c>RevisionVerseAudit</c> row (roadmap 0014) that copies the live row of <c>RevisionVerses</c> inside the caller's transaction.
/// The mapping snapshot is built from the live words and mapping rows, keyed by word position (the word ids change on text saves);
/// because it is read from the same rows the newest audit row of a live verse always equals the live state.
/// </summary>
public static class RevisionAuditWriter
{
    /// <summary>Snapshot columns: position, stepWordId, isAddedWord, parentPosition, germanWordPart, origin (first letter: c, m, s).</summary>
    public const string Insert = """
                                 INSERT INTO "unshackled-word"."RevisionVerseAudit"
                                     ("RevisionVerseId", "HebRefId", "RevisionId", "Version", "Action", "VerseText", "Note", "BasedOnRevisionId", "BasedOnVersion", "BaseTextHash", "Mapping", "ByUserId")
                                 SELECT rv."Id"
                                      , rv."HebRefId"
                                      , rv."RevisionId"
                                      , rv."Version"
                                      , @Action
                                      , rv."VerseText"
                                      , rv."Note"
                                      , rv."BasedOnRevisionId"
                                      , rv."BasedOnVersion"
                                      , rv."BaseTextHash"
                                      , CASE WHEN @Action = 'withdraw'
                                          THEN NULL
                                          ELSE COALESCE((SELECT jsonb_agg(jsonb_build_array(rw."PositionInVerse", rm."StepWordId", rm."IsAddedWord", pw."PositionInVerse", rm."GermanWordPart", left(rm."Origin", 1)) ORDER BY rw."PositionInVerse", rm."Id")
                                                         FROM "unshackled-word"."RevisionWords" rw
                                                             JOIN "unshackled-word"."RevisionMapping" rm ON rm."RevisionWordId" = rw."Id"
                                                             LEFT JOIN "unshackled-word"."RevisionWords" pw ON pw."Id" = rm."ParentRevisionWordId"
                                                         WHERE rw."RevisionVerseId" = rv."Id"), '[]'::jsonb)
                                        END
                                      , @ByUserId
                                 FROM "unshackled-word"."RevisionVerses" rv
                                 WHERE rv."Id" = @RevisionVerseId
                                 """;

    /// <summary>Writes the audit row for the current state of <paramref name="revisionVerseId"/>; <paramref name="byUserId"/> null = seed import.</summary>
    public static Task WriteAsync(IDbConnection connection, IDbTransaction transaction, int revisionVerseId, string action, int? byUserId) =>
        connection.ExecuteAsync(Insert, new { RevisionVerseId = revisionVerseId, Action = action, ByUserId = byUserId }, transaction);
}
