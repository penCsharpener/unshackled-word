using System.Text.Json;
using Dapper;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.WebApi.Lemmas;
using UnshackledWord.Tooling.WebApi.Endpoints.Lemmas.Stts;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Lemmas;

/// <summary>Data access of the German lemma editor (Elb1871Words + Elb1871Morphology).</summary>
public interface ILemmaRepository
{
    Task<IReadOnlyList<LemmaWordRow>> GetChapterAsync(int bookId, int chapter, CancellationToken ct);

    /// <summary>
    /// Applies all changes in one transaction (upserting the morphology row, writing an audit row each).
    /// Returns the ids that do not exist; when that list is non-empty nothing was written.
    /// </summary>
    Task<IReadOnlyList<int>> ApplyAsync(IReadOnlyList<LemmaWordChange> changes, int userId, CancellationToken ct);

    Task<IReadOnlyList<LemmaWordRow>> GetVerseAsync(int bookId, int chapter, int verse, CancellationToken ct);

    Task<IReadOnlyList<LemmaWordRow>> GetWordsAsync(IReadOnlyCollection<int> wordIds, CancellationToken ct);
}

public sealed class LemmaRepository : ILemmaRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    private readonly ISttsTagRepository _sttsTags;

    public LemmaRepository(IDbConnectionFactory connectionFactory, ISttsTagRepository sttsTags)
    {
        _connectionFactory = connectionFactory;
        _sttsTags = sttsTags;
    }

    private const string SelectRows = """
        SELECT w."Id"                  AS "WordId"
             , w."Verse"               AS "Verse"
             , w."PositionInVerse"     AS "PositionInVerse"
             , w."WordInContext"       AS "WordInContext"
             , w."PlainWord"           AS "PlainWord"
             , m."Lemma"                 AS "Lemma"
             , m."PartOfSpeech"          AS "PartOfSpeech"
             , m."Stts"                AS "Stts"
             , m."Degree"              AS "Degree"
             , m."VerbForm"            AS "VerbForm"
             , m."Tense"               AS "Tense"
             , m."Person"              AS "Person"
             , m."Number"              AS "Number"
             , m."Mood"                AS "Mood"
             , m."Case"                AS "Case"
             , m."Gender"              AS "Gender"
             , (m."Id" IS NOT NULL)    AS "HasMorphology"
        FROM "unshackled-word"."Elb1871Words" w
            LEFT JOIN "unshackled-word"."Elb1871Morphology" m ON m."HebRefId" = w."HebRefId" AND m."PositionInVerse" = w."PositionInVerse"
        """;

    private const string SelectChapter = SelectRows + """

        WHERE w."BibleBookId" = @bookId
            AND w."Chapter" = @chapter
        ORDER BY w."Verse", w."PositionInVerse"
        """;

    private const string SelectVerse = SelectRows + """

        WHERE w."BibleBookId" = @bookId
            AND w."Chapter" = @chapter
            AND w."Verse" = @verse
        ORDER BY w."PositionInVerse"
        """;

    private const string SelectByIds = SelectRows + """

        WHERE w."Id" = ANY(@ids)
        ORDER BY w."Verse", w."PositionInVerse"
        """;

    private const string SelectWordForEdit = """
        SELECT w."HebRefId"        AS "HebRefId"
             , w."PositionInVerse" AS "PositionInVerse"
        FROM "unshackled-word"."Elb1871Words" w
        WHERE w."Id" = @wordId
        """;

    private const string SelectMorphologyBefore = """
        SELECT m."Lemma"        AS "Lemma"
             , m."PartOfSpeech" AS "PartOfSpeech"
             , m."Stts"         AS "Stts"
             , m."Degree"       AS "Degree"
             , m."VerbForm"     AS "VerbForm"
             , m."Tense"        AS "Tense"
             , m."Person"       AS "Person"
             , m."Number"       AS "Number"
             , m."Mood"         AS "Mood"
             , m."Case"         AS "Case"
             , m."Gender"       AS "Gender"
        FROM "unshackled-word"."Elb1871Morphology" m
        WHERE m."HebRefId" = @hebRefId
            AND m."PositionInVerse" = @position
        FOR UPDATE
        """;

    private const string UpsertMorphology = """
        INSERT INTO "unshackled-word"."Elb1871Morphology" AS m ("HebRefId", "PositionInVerse", "Lemma", "PartOfSpeech", "Stts", "Degree", "VerbForm", "Tense", "Person", "Number", "Mood", "Case", "Gender")
        VALUES (@HebRefId, @PositionInVerse, @Lemma, @PartOfSpeech, @Stts, @Degree, @VerbForm, @Tense, @Person, @Number, @Mood, @Case, @Gender)
        ON CONFLICT ("HebRefId", "PositionInVerse") DO UPDATE
        SET "Lemma" = EXCLUDED."Lemma"
          , "PartOfSpeech" = EXCLUDED."PartOfSpeech"
          , "Stts" = COALESCE(EXCLUDED."Stts", m."Stts")
          , "Degree" = EXCLUDED."Degree"
          , "VerbForm" = EXCLUDED."VerbForm"
          , "Tense" = EXCLUDED."Tense"
          , "Person" = EXCLUDED."Person"
          , "Number" = EXCLUDED."Number"
          , "Mood" = EXCLUDED."Mood"
          , "Case" = EXCLUDED."Case"
          , "Gender" = EXCLUDED."Gender"
        """;

    // Keeps Elb1871Verses."LemmaText" (and with it the verse search vector) in step with the morphology rows written in the same transaction.
    private const string RefreshVerseLemmaText = """
        SELECT "unshackled-word"."RefreshVerseLemmaText"(@hebRefIds)
        """;

    private const string InsertAudit = """
        INSERT INTO "unshackled-word"."Elb1871MorphologyEdits" ("ElbWordId", "HebRefId", "PositionInVerse", "UserId", "BeforeValues", "AfterValues")
        VALUES (@wordId, @hebRefId, @position, @userId, @before::jsonb, @after::jsonb)
        """;

    public async Task<IReadOnlyList<LemmaWordRow>> GetChapterAsync(int bookId, int chapter, CancellationToken ct)
    {
        using var connection = await _connectionFactory.CreateDbConnectionAsync(ct);
        var rows = await connection.QueryAsync<LemmaWordRow>(new CommandDefinition(SelectChapter, new { bookId, chapter }, cancellationToken: ct));
        return await WithProposalsAsync(rows, ct);
    }

    public async Task<IReadOnlyList<LemmaWordRow>> GetVerseAsync(int bookId, int chapter, int verse, CancellationToken ct)
    {
        using var connection = await _connectionFactory.CreateDbConnectionAsync(ct);
        var rows = await connection.QueryAsync<LemmaWordRow>(new CommandDefinition(SelectVerse, new { bookId, chapter, verse }, cancellationToken: ct));
        return await WithProposalsAsync(rows, ct);
    }

    public async Task<IReadOnlyList<LemmaWordRow>> GetWordsAsync(IReadOnlyCollection<int> wordIds, CancellationToken ct)
    {
        using var connection = await _connectionFactory.CreateDbConnectionAsync(ct);
        var rows = await connection.QueryAsync<LemmaWordRow>(new CommandDefinition(SelectByIds, new { ids = wordIds.ToArray() }, cancellationToken: ct));
        return await WithProposalsAsync(rows, ct);
    }

    private async Task<IReadOnlyList<LemmaWordRow>> WithProposalsAsync(IEnumerable<LemmaWordRow> rows, CancellationToken ct)
    {
        var list = rows.ToList();
        if (list.Any(r => r.Stts is not null)) SttsRules.AddProposals(list, new SttsCatalog(await _sttsTags.GetAllAsync(ct)));
        return list;
    }

    public async Task<IReadOnlyList<int>> ApplyAsync(IReadOnlyList<LemmaWordChange> changes, int userId, CancellationToken ct)
    {
        using var connection = await _connectionFactory.CreateDbConnectionAsync(ct);
        using var transaction = connection.BeginTransaction();

        var missing = new List<int>();
        var touchedVerses = new HashSet<int>();

        foreach (var change in changes)
        {
            var word = await connection.QuerySingleOrDefaultAsync<WordKey>(
                new CommandDefinition(SelectWordForEdit, new { wordId = change.WordId }, transaction, cancellationToken: ct));

            if (word is null)
            {
                missing.Add(change.WordId);
                continue;
            }

            touchedVerses.Add(word.HebRefId);

            var before = await connection.QuerySingleOrDefaultAsync<LemmaEditFields>(
                new CommandDefinition(SelectMorphologyBefore, new { hebRefId = word.HebRefId, position = word.PositionInVerse }, transaction, cancellationToken: ct));

            await connection.ExecuteAsync(new CommandDefinition(UpsertMorphology, new
            {
                word.HebRefId,
                word.PositionInVerse,
                Lemma = change.Lemma.Trim(),
                change.PartOfSpeech,
                change.Stts,
                change.Degree,
                change.VerbForm,
                change.Tense,
                change.Person,
                change.Number,
                change.Mood,
                change.Case,
                change.Gender,
            }, transaction, cancellationToken: ct));

            await connection.ExecuteAsync(new CommandDefinition(InsertAudit, new
            {
                wordId = change.WordId,
                hebRefId = word.HebRefId,
                position = word.PositionInVerse,
                userId,
                before = before is null ? null : JsonSerializer.Serialize(before),
                after = JsonSerializer.Serialize(new LemmaEditFields
                {
                    Lemma = change.Lemma.Trim(),
                    PartOfSpeech = change.PartOfSpeech,
                    Stts = change.Stts ?? before?.Stts,
                    Degree = change.Degree,
                    VerbForm = change.VerbForm,
                    Tense = change.Tense,
                    Person = change.Person,
                    Number = change.Number,
                    Mood = change.Mood,
                    Case = change.Case,
                    Gender = change.Gender,
                }),
            }, transaction, cancellationToken: ct));
        }

        if (missing.Count > 0)
        {
            transaction.Rollback();
            return missing;
        }

        await connection.ExecuteAsync(new CommandDefinition(RefreshVerseLemmaText, new { hebRefIds = touchedVerses.ToArray() }, transaction, cancellationToken: ct));

        transaction.Commit();
        return missing;
    }

    private sealed class WordKey
    {
        public int HebRefId { get; set; }
        public int PositionInVerse { get; set; }
    }
}
