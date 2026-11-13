using UnshackledWord.Infrastructure.Seed;
using System.Data;
using Dapper;
using UnshackledWord.Application.Abstractions;

namespace UnshackledWord.Tooling.SeedDb.Services.RepoSeed.Morphology;

/// <summary>One row to insert into <c>Elb1871Morphology</c>; empty optional values are already null.</summary>
public sealed record MorphologyInsertRow(
    int HebRefId,
    int PositionInVerse,
    string Lemma,
    string PartOfSpeech,
    string? Stts,
    string? Degree,
    string? VerbForm,
    string? Tense,
    string? Person,
    string? Number,
    string? Mood,
    string? Case,
    string? Gender);

/// <summary>What <c>SeedImports</c> records about one import.</summary>
public sealed record SeedImportRecord(string Kind, string SeedVersion, DateTimeOffset? ExportedAt, int Rows, int Exact, int Shifted, int Modified, int Orphaned);

public sealed class MorphologySeedRepository
{
    private readonly IDbReader _dbReader;
    private readonly IDbConnectionFactory _connectionFactory;

    public MorphologySeedRepository(IDbReader dbReader, IDbConnectionFactory connectionFactory)
    {
        _dbReader = dbReader;
        _connectionFactory = connectionFactory;
    }

    private const string CountMorphology = """
        select
             count(*)
        from "unshackled-word"."Elb1871Morphology"
        """;

    private const string CountWords = """
        select
             count(*)
        from "unshackled-word"."Elb1871Words"
        """;

    public async Task<int> CountMorphologyAsync()
    {
        return await _dbReader.ExecuteScalarAsync<int>(CountMorphology);
    }

    public async Task<int> CountWordsAsync()
    {
        return await _dbReader.ExecuteScalarAsync<int>(CountWords);
    }

    /// <summary>
    /// Opens the single transaction of an import. Returns null when <c>Elb1871Morphology</c> is not empty (checked again under a table
    /// lock, so two seed containers cannot both import).
    /// </summary>
    public async Task<MorphologyImportSession?> BeginImportAsync(CancellationToken token)
    {
        var connection = await _connectionFactory.CreateDbConnectionAsync(token);
        var transaction = connection.BeginTransaction();

        await connection.ExecuteAsync("""LOCK TABLE "unshackled-word"."Elb1871Morphology" IN EXCLUSIVE MODE""", transaction: transaction);
        var existing = await connection.ExecuteScalarAsync<int>(CountMorphology, transaction: transaction);

        if (existing > 0)
        {
            transaction.Rollback();
            transaction.Dispose();
            connection.Dispose();

            return null;
        }

        return new MorphologyImportSession(connection, transaction);
    }
}

/// <summary>One import transaction: inserts, the verse lemma text refresh and the <c>SeedImports</c> row commit together or not at all.</summary>
public sealed class MorphologyImportSession : IDisposable
{
    private const int CommandTimeoutSeconds = 900;
    private const int BatchSize = 50_000;

    private const string InsertRows = """
        insert into "unshackled-word"."Elb1871Morphology"
            ("HebRefId", "PositionInVerse", "Lemma", "PartOfSpeech", "Stts", "Degree", "VerbForm", "Tense", "Person", "Number", "Mood", "Case", "Gender")
        select *
        from unnest(@HebRefId, @PositionInVerse, @Lemma, @PartOfSpeech, @Stts, @Degree, @VerbForm, @Tense, @Person, @Number, @Mood, @Case, @Gender)
        """;

    private const string RefreshLemmaText = """
        select
             "unshackled-word"."RefreshVerseLemmaText"(null) as "Changed"
        """;

    private const string InsertSeedImport = """
        insert into "unshackled-word"."SeedImports"
            ("Kind", "SeedVersion", "ExportedAt", "Rows", "Exact", "Shifted", "Modified", "Orphaned")
        values
            (@Kind, @SeedVersion, @ExportedAt, @Rows, @Exact, @Shifted, @Modified, @Orphaned)
        """;

    private readonly IDbConnection _connection;
    private readonly IDbTransaction _transaction;
    private bool _completed;

    public MorphologyImportSession(IDbConnection connection, IDbTransaction transaction)
    {
        _connection = connection;
        _transaction = transaction;
    }

    public async Task InsertAsync(IReadOnlyList<MorphologyInsertRow> rows)
    {
        for (var start = 0; start < rows.Count; start += BatchSize)
        {
            var batch = rows.Skip(start).Take(BatchSize).ToList();

            var parameters = new
            {
                HebRefId = batch.Select(r => r.HebRefId).ToArray(),
                PositionInVerse = batch.Select(r => r.PositionInVerse).ToArray(),
                Lemma = batch.Select(r => r.Lemma).ToArray(),
                PartOfSpeech = batch.Select(r => r.PartOfSpeech).ToArray(),
                Stts = batch.Select(r => r.Stts).ToArray(),
                Degree = batch.Select(r => r.Degree).ToArray(),
                VerbForm = batch.Select(r => r.VerbForm).ToArray(),
                Tense = batch.Select(r => r.Tense).ToArray(),
                Person = batch.Select(r => r.Person).ToArray(),
                Number = batch.Select(r => r.Number).ToArray(),
                Mood = batch.Select(r => r.Mood).ToArray(),
                Case = batch.Select(r => r.Case).ToArray(),
                Gender = batch.Select(r => r.Gender).ToArray()
            };

            await _connection.ExecuteAsync(InsertRows, parameters, _transaction, CommandTimeoutSeconds);
        }
    }

    /// <summary>
    /// Fills <c>Elb1871Verses."LemmaText"</c> for every verse (function of migration 0016; without it the smart search has no lemma
    /// widening on a fresh install), records the import and commits. Returns the number of verses whose lemma text changed.
    /// </summary>
    public async Task<int> CompleteAsync(SeedImportRecord record)
    {
        var changed = await _connection.ExecuteScalarAsync<int>(RefreshLemmaText, transaction: _transaction, commandTimeout: CommandTimeoutSeconds);
        await _connection.ExecuteAsync(InsertSeedImport, record, _transaction, CommandTimeoutSeconds);

        _transaction.Commit();
        _completed = true;

        return changed;
    }

    public void Dispose()
    {
        if (!_completed)
        {
            _transaction.Rollback();
        }

        _transaction.Dispose();
        _connection.Dispose();
    }
}
