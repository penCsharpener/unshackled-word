using Dapper;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Features.AgentMapping;
using UnshackledWord.Application.Features.AgentMapping.Lxx;

namespace UnshackledWord.Infrastructure.Lxx;

/// <summary>Reads of the LXX agent mode (roadmap 0028.7): units of a Rahlfs range, their words with tags, the Hebrew words, TBESG and the NT forms. The SQL is public for the database tests.</summary>
public sealed class LxxAgentDataRepository : ILxxAgentDataRepository
{
    private readonly IDbConnectionFactory _factory;

    public LxxAgentDataRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<LxxAgentRangeData> GetRangeAsync(int bibleBookId, int fromChapter, int fromVerse, int toChapter, int toVerse, CancellationToken token = default)
    {
        using var connection = await _factory.CreateDbConnectionAsync(token);
        var units = (await connection.QueryAsync<LxxAgentUnitRow>(new CommandDefinition(LxxAgentSql.RangeUnits, new
        {
            BibleBookId = bibleBookId,
            FromChapter = fromChapter,
            FromVerse = fromVerse,
            ToChapter = toChapter,
            ToVerse = toVerse,
        }, cancellationToken: token))).ToList();

        return await WithWordsAsync(connection, units, token);
    }

    public async Task<LxxAgentRangeData> GetUnitsAsync(IReadOnlyCollection<int> lxxVerseIds, CancellationToken token = default)
    {
        using var connection = await _factory.CreateDbConnectionAsync(token);
        var units = (await connection.QueryAsync<LxxAgentUnitRow>(new CommandDefinition(LxxAgentSql.UnitsById, new { Ids = lxxVerseIds.ToArray() }, cancellationToken: token))).ToList();

        return await WithWordsAsync(connection, units, token);
    }

    public async Task<IReadOnlyList<LxxLexiconEntry>> GetLexiconAsync(CancellationToken token = default)
    {
        using var connection = await _factory.CreateDbConnectionAsync(token);
        var rows = await connection.QueryAsync<(string Strongs, string Lemma, string Gloss)>(new CommandDefinition(LxxAgentSql.Lexicon, cancellationToken: token));

        return rows.Select(r => new LxxLexiconEntry(r.Strongs, r.Lemma, r.Gloss)).ToList();
    }

    public async Task<IReadOnlyList<LxxNtFormCount>> GetNtFormsAsync(CancellationToken token = default)
    {
        using var connection = await _factory.CreateDbConnectionAsync(token);
        var rows = await connection.QueryAsync<(string Greek, string Strongs, int Count)>(new CommandDefinition(LxxAgentSql.NtForms, cancellationToken: token));

        return rows.Select(r => new LxxNtFormCount(r.Greek, r.Strongs, r.Count)).ToList();
    }

    public async Task<IReadOnlySet<int>> GetProposedUnitIdsAsync(int runId, CancellationToken token = default)
    {
        using var connection = await _factory.CreateDbConnectionAsync(token);
        var ids = await connection.QueryAsync<int>(new CommandDefinition(LxxAgentSql.ProposedUnitIds, new { RunId = runId }, cancellationToken: token));

        return ids.ToHashSet();
    }

    private static async Task<LxxAgentRangeData> WithWordsAsync(System.Data.IDbConnection connection, List<LxxAgentUnitRow> units, CancellationToken token)
    {
        if (units.Count == 0)
        {
            return new LxxAgentRangeData();
        }

        var words = await connection.QueryAsync<LxxAgentWordRow>(new CommandDefinition(LxxAgentSql.Words, new { Ids = units.Select(u => u.Id).ToArray() }, cancellationToken: token));
        var hebRefIds = units.Where(u => u.HebRefId is not null).Select(u => u.HebRefId!.Value).Distinct().ToArray();
        var hebrew = hebRefIds.Length == 0
            ? []
            : await connection.QueryAsync<AgentSourceWordRow>(new CommandDefinition(LxxAgentSql.HebrewWords, new { HebRefIds = hebRefIds }, cancellationToken: token));

        return new LxxAgentRangeData { Units = units, Words = words.ToList(), HebrewWords = hebrew.ToList() };
    }
}

public static class LxxAgentSql
{
    private const string UnitColumns = """
                                       SELECT l."Id"
                                            , l."BibleBookId"
                                            , l."Chapter"
                                            , l."Verse"
                                            , l."Subverse"
                                            , l."TextForm"
                                            , l."HebRefId"
                                            , l."MapKind"
                                            , l."SortOrder"
                                       FROM "unshackled-word"."LxxVerses" l
                                       """;

    /// <summary>Units with a Rahlfs reference of one book, (chapter, verse) in range, every subverse and text form, Swete order.</summary>
    public const string RangeUnits = UnitColumns + """

                                                   WHERE 1=1
                                                       AND l."BibleBookId" = @BibleBookId
                                                       AND l."RahlfsRefId" IS NOT NULL
                                                       AND (l."Chapter", l."Verse") BETWEEN (@FromChapter, @FromVerse) AND (@ToChapter, @ToVerse)
                                                   ORDER BY l."SortOrder"
                                                   """;

    public const string UnitsById = UnitColumns + """

                                                  WHERE 1=1
                                                      AND l."Id" = ANY(@Ids)
                                                  ORDER BY l."SortOrder"
                                                  """;

    /// <summary>The words of the units with their current tag (any status or source).</summary>
    public const string Words = """
                                SELECT w."Id"
                                     , w."LxxVerseId"
                                     , w."Position"
                                     , w."Greek"
                                     , t."Lemma" "TagLemma"
                                     , t."Strongs" "TagStrongs"
                                     , t."Morphology" "TagMorphology"
                                     , t."Status" "TagStatus"
                                     , t."Source" "TagSource"
                                FROM "unshackled-word"."LxxWords" w
                                    LEFT JOIN "unshackled-word"."LxxWordTags" t ON t."LxxWordId" = w."Id"
                                WHERE 1=1
                                    AND w."LxxVerseId" = ANY(@Ids)
                                ORDER BY w."LxxVerseId"
                                       , w."Position"
                                """;

    /// <summary>The STEP Hebrew words of the units' Hebrew verses (<c>StepHebrewWords.LxxRefId</c> = Elberfelder HebRefId, as the mapping uses it).</summary>
    public const string HebrewWords = """
                                      SELECT shw."Id"
                                           , shw."LxxRefId" "RefId"
                                           , shw."PositionInVerse" "Position"
                                           , shw."Hebrew" "Text"
                                           , shw."RootDisambiguatedStrongsInstance" "Strongs"
                                           , shw."Gloss"
                                           , shw."Grammar" "Morphology"
                                           , shw."ExpandedStrongTags"
                                      FROM "unshackled-word"."StepHebrewWords" shw
                                      WHERE 1=1
                                          AND shw."LxxRefId" = ANY(@HebRefIds)
                                      ORDER BY shw."LxxRefId"
                                             , shw."PositionInVerse"
                                             , shw."Id"
                                      """;

    /// <summary>Greek TBESG with the Strong's key of LXX tags: G + at least four digits + the disambiguation letter (G0746, G1487G, G20969).</summary>
    public const string Lexicon = """
                                  SELECT 'G' || lpad(ssl."Number"::text, greatest(4, length(ssl."Number"::text)), '0') || COALESCE(ssl."Extra", '') "Strongs"
                                       , ssl."OriginalWord" "Lemma"
                                       , ssl."Gloss"
                                  FROM "unshackled-word"."StepStrongsLexicon" ssl
                                  WHERE 1=1
                                      AND ssl."LanguageId" = 2
                                  ORDER BY ssl."Number"
                                         , ssl."Extra" NULLS FIRST
                                         , ssl."Id"
                                  """;

    /// <summary>NT running-text forms per Strong's number with their counts (the candidate index normalises them).</summary>
    public const string NtForms = """
                                  SELECT sgw."Greek"
                                       , sgw."DisambiguatedStrongs" "Strongs"
                                       , count(*)::int "Count"
                                  FROM "unshackled-word"."StepGreekWords" sgw
                                  GROUP BY sgw."Greek"
                                         , sgw."DisambiguatedStrongs"
                                  """;

    /// <summary>LXX units of a run that already have a proposal (<c>Flags.lxxVerseId</c>, written by the LXX submit path).</summary>
    public const string ProposedUnitIds = """
                                          SELECT DISTINCT (p."Flags" ->> 'lxxVerseId')::int
                                          FROM "unshackled-word"."AgentVerseProposals" p
                                          WHERE 1=1
                                              AND p."RunId" = @RunId
                                              AND p."Language" = 'lxx'
                                              AND jsonb_typeof(p."Flags") = 'object'
                                              AND p."Flags" ->> 'lxxVerseId' IS NOT NULL
                                          """;
}
