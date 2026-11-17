using Dapper;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Features.AgentMapping;

namespace UnshackledWord.Infrastructure.Repositories;

/// <summary>Reads for the agent package builder and validator (roadmap 0016.3/0016.4). Elb1871Morphology rows match Elb1871Words by (HebRefId, PositionInVerse), never by id.</summary>
public sealed class AgentDataRepository : IAgentDataRepository
{
    private readonly IDbConnectionFactory _factory;

    public AgentDataRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<AgentRangeData> GetRangeAsync(int bibleBookId, int fromChapter, int fromVerse, int toChapter, int toVerse, CancellationToken token = default)
    {
        var hebrew = bibleBookId <= 39;
        var parameters = new { BibleBookId = bibleBookId, FromChapter = fromChapter, FromVerse = fromVerse, ToChapter = toChapter, ToVerse = toVerse };
        using var connection = await _factory.CreateDbConnectionAsync(token);

        var german = await connection.QueryAsync<AgentGermanWordRow>(new CommandDefinition(AgentSql.GermanWords, parameters, cancellationToken: token));
        var source = await connection.QueryAsync<AgentSourceWordRow>(new CommandDefinition(hebrew ? AgentSql.HebrewWords : AgentSql.GreekWords, parameters, cancellationToken: token));
        var mappings = await connection.QueryAsync<AgentMappingRow>(new CommandDefinition(hebrew ? AgentSql.HebrewMappings : AgentSql.GreekMappings, parameters, cancellationToken: token));

        return new AgentRangeData { GermanWords = german.ToList(), SourceWords = source.ToList(), Mappings = mappings.ToList() };
    }

    public async Task<IReadOnlyList<AgentFormLemmaRow>> GetFormLemmasAsync(IReadOnlyCollection<string> forms, CancellationToken token = default)
    {
        if (forms.Count == 0)
        {
            return [];
        }

        using var connection = await _factory.CreateDbConnectionAsync(token);
        using var transaction = await BeginWithoutParallelismAsync(connection, token);
        var parameters = new { Forms = forms.Select(f => f.ToLowerInvariant()).Distinct().ToArray() };
        var candidates = await connection.QueryAsync<AgentFormLemmaRow>(new CommandDefinition(AgentSql.FormLemmaCandidates, parameters, transaction, cancellationToken: token));
        var trusted = await connection.QueryAsync<AgentFormLemmaRow>(new CommandDefinition(AgentSql.FormLemmaTrusted, parameters, transaction, cancellationToken: token));

        return [.. trusted, .. candidates];
    }

    public async Task<IReadOnlyList<AgentConfirmedPairRow>> GetConfirmedPairsAsync(bool hebrew, IReadOnlyCollection<string> lemmasLower, CancellationToken token = default)
    {
        if (lemmasLower.Count == 0)
        {
            return [];
        }

        using var connection = await _factory.CreateDbConnectionAsync(token);
        using var transaction = await BeginWithoutParallelismAsync(connection, token);
        var rows = await connection.QueryAsync<AgentConfirmedPairRow>(new CommandDefinition(
            hebrew ? AgentSql.HebrewConfirmedPairs : AgentSql.GreekConfirmedPairs,
            new { Lemmas = lemmasLower.Select(l => l.ToLowerInvariant()).Distinct().ToArray() },
            transaction,
            cancellationToken: token));

        return rows.ToList();
    }

    /// <summary>
    /// The big form and pair aggregations read hundreds of thousands of rows; a parallel hash join would need a shared-memory segment (/dev/shm of a
    /// small Postgres container is 64 MB and has run full). One backend is slower but never fails for lack of shared memory.
    /// </summary>
    private static async Task<System.Data.IDbTransaction> BeginWithoutParallelismAsync(System.Data.IDbConnection connection, CancellationToken token)
    {
        var transaction = connection.BeginTransaction();
        await connection.ExecuteAsync(new CommandDefinition("SET LOCAL max_parallel_workers_per_gather = 0", transaction: transaction, cancellationToken: token));
        return transaction;
    }

    public async Task<IReadOnlySet<string>> GetKnownLemmasAsync(IReadOnlyCollection<string> lemmas, CancellationToken token = default)
    {
        if (lemmas.Count == 0)
        {
            return new HashSet<string>();
        }

        using var connection = await _factory.CreateDbConnectionAsync(token);
        var rows = await connection.QueryAsync<string>(new CommandDefinition(AgentSql.KnownLemmas, new { Lemmas = lemmas.Distinct().ToArray() }, cancellationToken: token));

        return rows.ToHashSet(StringComparer.Ordinal);
    }
}

/// <summary>SQL of the agent repositories, public so tests can assert its shape.</summary>
public static class AgentSql
{
    public const string GermanWords = """
                                      SELECT ew."Id"
                                           , ew."Chapter"
                                           , ew."Verse"
                                           , ew."HebRefId"
                                           , ew."PositionInVerse"
                                           , ew."WordInContext" "Text"
                                           , COALESCE(ew."PlainWord", '') "Plain"
                                           , m."Lemma"
                                           , m."PartOfSpeech"
                                           , m."Stts"
                                           , m."Person"
                                           , m."Number"
                                           , m."Gender"
                                           , m."Case"
                                           , EXISTS (SELECT 1
                                                     FROM "unshackled-word"."Elb1871MorphologyEdits" e
                                                     WHERE 1=1
                                                         AND e."HebRefId" = ew."HebRefId"
                                                         AND e."PositionInVerse" = ew."PositionInVerse"
                                                         AND e."AfterValues" ->> 'Lemma' = m."Lemma"
                                                         AND NOT EXISTS (SELECT 1
                                                                         FROM "unshackled-word"."UserRoles" ur
                                                                         WHERE 1=1
                                                                             AND ur."UserId" = e."UserId"
                                                                             AND ur."Role" = 'ai')) "LemmaHumanEdited"
                                      FROM "unshackled-word"."Elb1871Words" ew
                                          LEFT JOIN "unshackled-word"."Elb1871Morphology" m ON m."HebRefId" = ew."HebRefId" AND m."PositionInVerse" = ew."PositionInVerse"
                                      WHERE 1=1
                                          AND ew."BibleBookId" = @BibleBookId
                                          AND (ew."Chapter", ew."Verse") BETWEEN (@FromChapter, @FromVerse) AND (@ToChapter, @ToVerse)
                                      ORDER BY ew."Chapter"
                                             , ew."Verse"
                                             , ew."PositionInVerse"
                                             , ew."Id"
                                      """;

    public const string GreekWords = """
                                     SELECT sgw."Id"
                                          , sgw."LxxRefId" "RefId"
                                          , sgw."PositionInVerse" "Position"
                                          , sgw."Greek" "Text"
                                          , sgw."Lemma"
                                          , sgw."DisambiguatedStrongs" "Strongs"
                                          , sgw."Gloss"
                                          , sgw."Morphology"
                                     FROM "unshackled-word"."StepGreekWords" sgw
                                     WHERE 1=1
                                         AND sgw."LxxRefId" IN (SELECT DISTINCT ew."HebRefId"
                                                                FROM "unshackled-word"."Elb1871Words" ew
                                                                WHERE 1=1
                                                                    AND ew."BibleBookId" = @BibleBookId
                                                                    AND (ew."Chapter", ew."Verse") BETWEEN (@FromChapter, @FromVerse) AND (@ToChapter, @ToVerse))
                                     ORDER BY sgw."LxxRefId"
                                            , sgw."PositionInVerse"
                                            , sgw."Id"
                                     """;

    public const string HebrewWords = """
                                      SELECT shw."Id"
                                           , shw."LxxRefId" "RefId"
                                           , shw."PositionInVerse" "Position"
                                           , shw."Hebrew" "Text"
                                           , lex."OriginalWord" "Lemma"
                                           , shw."RootDisambiguatedStrongsInstance" "Strongs"
                                           , shw."Gloss"
                                           , shw."Grammar" "Morphology"
                                          , shw."ExpandedStrongTags"
                                      FROM "unshackled-word"."StepHebrewWords" shw
                                          LEFT JOIN LATERAL (SELECT ssl."OriginalWord"
                                                             FROM "unshackled-word"."StepStrongsLexicon" ssl
                                                             WHERE 1=1
                                                                 AND ssl."LanguageId" IN (0, 1)
                                                                 AND ssl."Number" = (regexp_match(shw."RootDisambiguatedStrongsInstance", '^H0*(\d+)'))[1]::int
                                                             ORDER BY (ssl."Extra" IS NOT DISTINCT FROM (regexp_match(shw."RootDisambiguatedStrongsInstance", '^H\d+([A-Za-z])'))[1]) DESC
                                                                    , ssl."Extra"
                                                             LIMIT 1) lex ON TRUE
                                      WHERE 1=1
                                          AND shw."LxxRefId" IN (SELECT DISTINCT ew."HebRefId"
                                                                 FROM "unshackled-word"."Elb1871Words" ew
                                                                 WHERE 1=1
                                                                     AND ew."BibleBookId" = @BibleBookId
                                                                     AND (ew."Chapter", ew."Verse") BETWEEN (@FromChapter, @FromVerse) AND (@ToChapter, @ToVerse))
                                      ORDER BY shw."LxxRefId"
                                             , shw."PositionInVerse"
                                             , shw."Id"
                                      """;

    public const string GreekMappings = """
                                        SELECT egm."ElbWordId"
                                             , egm."StepWordId"
                                             , COALESCE(egm."IsAddedWord", FALSE) "IsAddedWord"
                                             , egm."ParentGermanWordId" "ParentElbWordId"
                                             , egm."GermanWordPart"
                                             , egm."PartOrder"
                                             , egm."CreatedByUserId"
                                            , egm."Role"
                                            , egm."StrongsNumber"
                                            , egm."PartLemma"
                                            , egm."MultiHead"
                                             , COALESCE(EXISTS (SELECT 1
                                                                FROM "unshackled-word"."UserRoles" ur
                                                                WHERE 1=1
                                                                    AND ur."UserId" = egm."CreatedByUserId"
                                                                    AND ur."Role" = 'ai'), FALSE) "AuthorIsAi"
                                        FROM "unshackled-word"."Elb1871GreekMapping" egm
                                            JOIN "unshackled-word"."Elb1871Words" ew ON ew."Id" = egm."ElbWordId"
                                        WHERE 1=1
                                            AND ew."BibleBookId" = @BibleBookId
                                            AND (ew."Chapter", ew."Verse") BETWEEN (@FromChapter, @FromVerse) AND (@ToChapter, @ToVerse)
                                        ORDER BY ew."Chapter"
                                               , ew."Verse"
                                               , ew."PositionInVerse"
                                               , egm."Id"
                                        """;

    public const string HebrewMappings = """
                                         SELECT ehm."ElbWordId"
                                              , ehm."StepWordId"
                                              , COALESCE(ehm."IsAddedWord", FALSE) "IsAddedWord"
                                              , ehm."ParentGermanWordId" "ParentElbWordId"
                                              , ehm."GermanWordPart"
                                              , ehm."PartOrder"
                                              , ehm."CreatedByUserId"
                                             , ehm."Role"
                                             , ehm."StrongsNumber"
                                             , ehm."PartLemma"
                                             , ehm."MultiHead"
                                              , COALESCE(EXISTS (SELECT 1
                                                                 FROM "unshackled-word"."UserRoles" ur
                                                                 WHERE 1=1
                                                                     AND ur."UserId" = ehm."CreatedByUserId"
                                                                     AND ur."Role" = 'ai'), FALSE) "AuthorIsAi"
                                         FROM "unshackled-word"."Elb1871HebrewMapping" ehm
                                             JOIN "unshackled-word"."Elb1871Words" ew ON ew."Id" = ehm."ElbWordId"
                                         WHERE 1=1
                                             AND ew."BibleBookId" = @BibleBookId
                                             AND (ew."Chapter", ew."Verse") BETWEEN (@FromChapter, @FromVerse) AND (@ToChapter, @ToVerse)
                                         ORDER BY ew."Chapter"
                                                , ew."Verse"
                                                , ew."PositionInVerse"
                                                , ehm."Id"
                                         """;

    /// <summary>LemmaSharp output (and everything else in Elb1871Morphology): every lemma of a form with its word count.</summary>
    public const string FormLemmaCandidates = """
                                              SELECT lower(ew."PlainWord") "Form"
                                                   , m."Lemma"
                                                   , count(*)::int "Count"
                                                   , FALSE "Trusted"
                                              FROM "unshackled-word"."Elb1871Words" ew
                                                  JOIN "unshackled-word"."Elb1871Morphology" m ON m."HebRefId" = ew."HebRefId" AND m."PositionInVerse" = ew."PositionInVerse"
                                              WHERE 1=1
                                                  AND lower(ew."PlainWord") = ANY(@Forms)
                                              GROUP BY lower(ew."PlainWord")
                                                     , m."Lemma"
                                              """;

    /// <summary>Words of the form whose current lemma is the result of an edit (human, or an AI proposal a maintainer merged: both are audited in the edits table).</summary>
    public const string FormLemmaTrusted = """
                                           SELECT lower(ew."PlainWord") "Form"
                                                , m."Lemma"
                                                , count(DISTINCT ew."Id")::int "Count"
                                                , TRUE "Trusted"
                                           FROM "unshackled-word"."Elb1871MorphologyEdits" e
                                               JOIN "unshackled-word"."Elb1871Words" ew ON ew."HebRefId" = e."HebRefId" AND ew."PositionInVerse" = e."PositionInVerse"
                                               JOIN "unshackled-word"."Elb1871Morphology" m ON m."HebRefId" = ew."HebRefId" AND m."PositionInVerse" = ew."PositionInVerse"
                                           WHERE 1=1
                                               AND e."AfterValues" ->> 'Lemma' = m."Lemma"
                                               AND lower(ew."PlainWord") = ANY(@Forms)
                                           GROUP BY lower(ew."PlainWord")
                                                  , m."Lemma"
                                           """;

    public const string GreekConfirmedPairs = """
                                              SELECT lower(m."Lemma") "Lemma"
                                                   , sgw."DisambiguatedStrongs" "Strongs"
                                                   , count(*)::int "Count"
                                              FROM "unshackled-word"."Elb1871GreekMapping" egm
                                                  JOIN "unshackled-word"."Elb1871Words" ew ON ew."Id" = egm."ElbWordId"
                                                  JOIN "unshackled-word"."Elb1871Morphology" m ON m."HebRefId" = ew."HebRefId" AND m."PositionInVerse" = ew."PositionInVerse"
                                                  JOIN "unshackled-word"."StepGreekWords" sgw ON sgw."Id" = egm."StepWordId"
                                              WHERE 1=1
                                                  AND egm."CreatedByUserId" IS NOT NULL
                                                  AND NOT EXISTS (SELECT 1
                                                                  FROM "unshackled-word"."UserRoles" ur
                                                                  WHERE 1=1
                                                                      AND ur."UserId" = egm."CreatedByUserId"
                                                                      AND ur."Role" = 'ai')
                                                  AND lower(m."Lemma") = ANY(@Lemmas)
                                              GROUP BY lower(m."Lemma")
                                                     , sgw."DisambiguatedStrongs"
                                              """;

    public const string HebrewConfirmedPairs = """
                                               SELECT lower(m."Lemma") "Lemma"
                                                    , shw."RootDisambiguatedStrongsInstance" "Strongs"
                                                    , count(*)::int "Count"
                                               FROM "unshackled-word"."Elb1871HebrewMapping" ehm
                                                   JOIN "unshackled-word"."Elb1871Words" ew ON ew."Id" = ehm."ElbWordId"
                                                   JOIN "unshackled-word"."Elb1871Morphology" m ON m."HebRefId" = ew."HebRefId" AND m."PositionInVerse" = ew."PositionInVerse"
                                                   JOIN "unshackled-word"."StepHebrewWords" shw ON shw."Id" = ehm."StepWordId"
                                               WHERE 1=1
                                                   AND ehm."CreatedByUserId" IS NOT NULL
                                                   AND NOT EXISTS (SELECT 1
                                                                   FROM "unshackled-word"."UserRoles" ur
                                                                   WHERE 1=1
                                                                       AND ur."UserId" = ehm."CreatedByUserId"
                                                                       AND ur."Role" = 'ai')
                                                   AND lower(m."Lemma") = ANY(@Lemmas)
                                               GROUP BY lower(m."Lemma")
                                                      , shw."RootDisambiguatedStrongsInstance"
                                               """;

    public const string KnownLemmas = """
                                      SELECT DISTINCT m."Lemma"
                                      FROM "unshackled-word"."Elb1871Morphology" m
                                      WHERE 1=1
                                          AND m."Lemma" = ANY(@Lemmas)
                                      """;
}
