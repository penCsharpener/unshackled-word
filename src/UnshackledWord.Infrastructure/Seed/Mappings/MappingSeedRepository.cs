using UnshackledWord.Infrastructure.Seed;
using System.Data;
using Dapper;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Features.Seed.Anchoring;

namespace UnshackledWord.Infrastructure.Seed.Mappings;

/// <summary>A German word of this installation with its database id.</summary>
public sealed class ElbWordRow
{
    public int Id { get; init; }
    public int Ref { get; init; }
    public int Pos { get; init; }
    public string? Form { get; init; }
    public string? Text { get; init; }
}

/// <summary>A STEP word of this installation with its database id and its text (hashed with <see cref="SourceKey.Hash"/>, never stored).</summary>
public sealed class StepWordRow
{
    public int Id { get; init; }
    public int Ref { get; init; }
    public int Pos { get; init; }
    public string? Text { get; init; }
}

/// <summary>One mapping edge to write: the German word, its STEP word and parent as database ids plus the keys the verse hash needs.</summary>
public sealed record MappingRowPlan(
    int HebRefId,
    int PositionInVerse,
    int ElbWordId,
    int? StepWordId,
    int SrcRef,
    int SrcPos,
    string? Part,
    bool Added,
    int? ParentGermanWordId,
    int ParentRef,
    int ParentPos,
    int? PartOrder = null,
    string Role = "head",
    string? Strongs = null,
    string? PartLemma = null,
    bool MultiHead = false);

/// <summary>What <c>SeedImports</c> records about one mapping import or update.</summary>
public sealed record MappingImportRecord(
    string SeedVersion,
    DateTimeOffset? ExportedAt,
    int Rows,
    int Exact,
    int Shifted,
    int Modified,
    int Orphaned,
    int? VersesUpdated,
    int? VersesKeptLocal);

public sealed class LatestImportRow
{
    public string SeedVersion { get; init; } = string.Empty;
    public DateTimeOffset ImportedAt { get; init; }
}

public sealed class VerseHashRow
{
    public int HebRefId { get; init; }
    public string ContentHash { get; init; } = string.Empty;
}

/// <summary>Database access of the mapping seed import and update (legacy tables <c>Elb1871GreekMapping</c> / <c>Elb1871HebrewMapping</c>).</summary>
public sealed class MappingSeedRepository
{
    public const string Kind = "mappings";

    private const int CommandTimeoutSeconds = 900;
    private const string NoParallelWorkers = "set local max_parallel_workers_per_gather = 0";

    private readonly IDbReader _dbReader;
    private readonly IDbConnectionFactory _connectionFactory;

    public MappingSeedRepository(IDbReader dbReader, IDbConnectionFactory connectionFactory)
    {
        _dbReader = dbReader;
        _connectionFactory = connectionFactory;
    }

    private const string CountMappings = """
        select
             (select count(*) from "unshackled-word"."Elb1871GreekMapping") + (select count(*) from "unshackled-word"."Elb1871HebrewMapping")
        """;

    private const string SelectBookWords = """
        select
             w."Id"              as "Id"
            ,w."HebRefId"        as "Ref"
            ,w."PositionInVerse" as "Pos"
            ,w."PlainWord"       as "Form"
            ,w."WordInContext"   as "Text"
        from "unshackled-word"."Elb1871Words" w
        where w."BibleBookId" = @bookId
        order by w."HebRefId", w."PositionInVerse"
        """;

    private const string SelectGreekWords = """
        select
             s."Id"              as "Id"
            ,s."LxxRefId"        as "Ref"
            ,s."PositionInVerse" as "Pos"
            ,s."Greek"           as "Text"
        from "unshackled-word"."StepGreekWords" s
        where s."LxxRefId" = any(@refs)
        order by s."LxxRefId", s."PositionInVerse"
        """;

    private const string SelectHebrewWords = """
        select
             s."Id"              as "Id"
            ,s."LxxRefId"        as "Ref"
            ,s."PositionInVerse" as "Pos"
            ,s."Hebrew"          as "Text"
        from "unshackled-word"."StepHebrewWords" s
        where s."LxxRefId" = any(@refs)
        order by s."LxxRefId", s."PositionInVerse"
        """;

    private const string SelectLatestImport = """
        select
             i."SeedVersion" as "SeedVersion"
            ,i."ImportedAt"  as "ImportedAt"
        from "unshackled-word"."SeedImports" i
        where i."Kind" = @kind
        order by i."ImportedAt" desc, i."Id" desc
        limit 1
        """;

    private const string SelectState = """
        select
             s."HebRefId"    as "HebRefId"
            ,s."ContentHash" as "ContentHash"
        from "unshackled-word"."SeedVerseState" s
        where s."Kind" = @kind
        """;

    /// <summary>The verse hash formula of <c>MappingVerseHash</c> in SQL (roles, morpheme, part lemma and multi-head only when not default); all verses when <c>@refs</c> is null, otherwise only those.</summary>
    private const string SelectVerseHashes = """
        with edges as (
            select "ElbWordId", "StepWordId", "GermanWordPart", "IsAddedWord", "ParentGermanWordId", "Role", null::varchar as "StrongsNumber", "PartLemma", "MultiHead", true  as "IsGreek" from "unshackled-word"."Elb1871GreekMapping"
            union all
            select "ElbWordId", "StepWordId", "GermanWordPart", "IsAddedWord", "ParentGermanWordId", "Role", "StrongsNumber", "PartLemma", "MultiHead", false as "IsGreek" from "unshackled-word"."Elb1871HebrewMapping"
        ), lines as (
            select
                 w."HebRefId" as "HebRefId"
                ,concat_ws('|', w."PositionInVerse", coalesce(e."GermanWordPart", ''), coalesce(sg."LxxRefId", sh."LxxRefId", 0), coalesce(sg."PositionInVerse", sh."PositionInVerse", 0), coalesce(e."IsAddedWord", false)::text, coalesce(p."HebRefId", 0), coalesce(p."PositionInVerse", 0))
                    || case when e."Role" <> 'head' then '|R:' || e."Role" else '' end
                    || case when coalesce(e."StrongsNumber", '') <> '' then '|S:' || e."StrongsNumber" else '' end
                    || case when coalesce(e."PartLemma", '') <> '' then '|L:' || e."PartLemma" else '' end
                    || case when e."MultiHead" then '|M' else '' end as "Line"
            from edges e
            join "unshackled-word"."Elb1871Words" w on w."Id" = e."ElbWordId"
            left join "unshackled-word"."StepGreekWords" sg on e."IsGreek" and sg."Id" = e."StepWordId"
            left join "unshackled-word"."StepHebrewWords" sh on not e."IsGreek" and sh."Id" = e."StepWordId"
            left join "unshackled-word"."Elb1871Words" p on p."Id" = e."ParentGermanWordId"
            where @refs::int4[] is null
               or w."HebRefId" = any(@refs)
        )
        select
             l."HebRefId" as "HebRefId"
            ,encode(sha256(convert_to(string_agg(l."Line", E'\n' order by l."Line" collate "C"), 'UTF8')), 'hex') as "ContentHash"
        from lines l
        group by l."HebRefId"
        """;

    public async Task<int> CountMappingRowsAsync()
    {
        return await _dbReader.ExecuteScalarAsync<int>(CountMappings);
    }

    public async Task<List<ElbWordRow>> ReadBookWordsAsync(int bookId)
    {
        return (await _dbReader.ReadAsListAsync<ElbWordRow>(SelectBookWords, new { bookId })).ToList();
    }

    public async Task<List<StepWordRow>> ReadSourceWordsAsync(bool greek, int[] lxxRefs)
    {
        return (await _dbReader.ReadAsListAsync<StepWordRow>(greek ? SelectGreekWords : SelectHebrewWords, new { refs = lxxRefs })).ToList();
    }

    public async Task<LatestImportRow?> ReadLatestImportAsync()
    {
        return await _dbReader.ReadFirstOrDefaultAsync<LatestImportRow>(SelectLatestImport, new { kind = Kind });
    }

    public async Task<Dictionary<int, string>> ReadStateAsync()
    {
        return (await _dbReader.ReadAsListAsync<VerseHashRow>(SelectState, new { kind = Kind })).ToDictionary(r => r.HebRefId, r => r.ContentHash);
    }

    /// <summary>
    /// The content hash of every verse that has mapping rows. Runs without parallel workers: a parallel plan over 700k rows needs more
    /// shared memory than the 64 MB /dev/shm of a default Postgres container.
    /// </summary>
    public async Task<Dictionary<int, string>> ReadAllVerseHashesAsync(CancellationToken token = default)
    {
        using var connection = await _connectionFactory.CreateDbConnectionAsync(token);
        using var transaction = connection.BeginTransaction();

        await connection.ExecuteAsync(NoParallelWorkers, transaction: transaction);
        var rows = await connection.QueryAsync<VerseHashRow>(SelectVerseHashes, new { refs = (int[]?)null }, transaction, CommandTimeoutSeconds);

        return rows.ToDictionary(r => r.HebRefId, r => r.ContentHash);
    }

    /// <summary>Opens the single transaction of an import or update; the mapping tables are locked so two seed containers cannot both write.</summary>
    public async Task<MappingWriteSession> BeginWriteAsync(CancellationToken token)
    {
        var connection = await _connectionFactory.CreateDbConnectionAsync(token);
        var transaction = connection.BeginTransaction();

        await connection.ExecuteAsync(NoParallelWorkers, transaction: transaction);
        await connection.ExecuteAsync(
            """LOCK TABLE "unshackled-word"."Elb1871GreekMapping", "unshackled-word"."Elb1871HebrewMapping" IN EXCLUSIVE MODE""",
            transaction: transaction);

        return new MappingWriteSession(connection, transaction);
    }

    internal static string VerseHashesSql => SelectVerseHashes;
}

/// <summary>One import or update transaction: row changes, verse state and the <c>SeedImports</c> row commit together or not at all.</summary>
public sealed class MappingWriteSession : IDisposable
{
    private const int CommandTimeoutSeconds = 900;
    private const int BatchSize = 50_000;

    private const string CountMappings = """
        select
             (select count(*) from "unshackled-word"."Elb1871GreekMapping") + (select count(*) from "unshackled-word"."Elb1871HebrewMapping")
        """;

    private const string DeleteGreekVerses = """
        delete from "unshackled-word"."Elb1871GreekMapping" m
        using "unshackled-word"."Elb1871Words" w
        where w."Id" = m."ElbWordId"
          and w."HebRefId" = any(@refs)
        """;

    private const string DeleteHebrewVerses = """
        delete from "unshackled-word"."Elb1871HebrewMapping" m
        using "unshackled-word"."Elb1871Words" w
        where w."Id" = m."ElbWordId"
          and w."HebRefId" = any(@refs)
        """;

    private const string InsertGreekRows = """
        insert into "unshackled-word"."Elb1871GreekMapping"
            ("ElbWordId", "StepWordId", "HebRefId", "PositionInVerse", "IsAddedWord", "ParentGermanWordId", "GermanWordPart", "PartOrder", "Role", "PartLemma", "MultiHead")
        select *
        from unnest(@ElbWordId, @StepWordId, @HebRefId, @PositionInVerse, @IsAddedWord, @ParentGermanWordId, @GermanWordPart, @PartOrder, @Role, @PartLemma, @MultiHead)
        on conflict ("ElbWordId", "StepWordId") do nothing
        """;

    private const string InsertHebrewRows = """
        insert into "unshackled-word"."Elb1871HebrewMapping"
            ("ElbWordId", "StepWordId", "HebRefId", "PositionInVerse", "IsAddedWord", "ParentGermanWordId", "GermanWordPart", "PartOrder", "Role", "PartLemma", "MultiHead", "StrongsNumber")
        select *
        from unnest(@ElbWordId, @StepWordId, @HebRefId, @PositionInVerse, @IsAddedWord, @ParentGermanWordId, @GermanWordPart, @PartOrder, @Role, @PartLemma, @MultiHead, @StrongsNumber)
        on conflict ("ElbWordId", "StepWordId") do nothing
        """;

    private const string UpsertState = """
        insert into "unshackled-word"."SeedVerseState"
            ("Kind", "HebRefId", "ContentHash", "SeedVersion")
        select @kind, r, h, @seedVersion
        from unnest(@refs, @hashes) as t(r, h)
        on conflict ("Kind", "HebRefId") do update
            set "ContentHash" = excluded."ContentHash"
               ,"SeedVersion" = excluded."SeedVersion"
        """;

    private const string DeleteState = """
        delete from "unshackled-word"."SeedVerseState"
        where "Kind" = @kind
          and "HebRefId" = any(@refs)
        """;

    private const string InsertSeedImport = """
        insert into "unshackled-word"."SeedImports"
            ("Kind", "SeedVersion", "ExportedAt", "Rows", "Exact", "Shifted", "Modified", "Orphaned", "VersesUpdated", "VersesKeptLocal")
        values
            (@Kind, @SeedVersion, @ExportedAt, @Rows, @Exact, @Shifted, @Modified, @Orphaned, @VersesUpdated, @VersesKeptLocal)
        """;

    private readonly IDbConnection _connection;
    private readonly IDbTransaction _transaction;
    private bool _completed;

    public MappingWriteSession(IDbConnection connection, IDbTransaction transaction)
    {
        _connection = connection;
        _transaction = transaction;
    }

    public async Task<int> CountMappingRowsAsync()
    {
        return await _connection.ExecuteScalarAsync<int>(CountMappings, transaction: _transaction);
    }

    /// <summary>Deletes the mapping rows of the given German verses from both tables.</summary>
    public async Task DeleteVersesAsync(int[] hebRefIds)
    {
        foreach (var batch in hebRefIds.Chunk(5_000))
        {
            await _connection.ExecuteAsync(DeleteGreekVerses, new { refs = batch }, _transaction, CommandTimeoutSeconds);
            await _connection.ExecuteAsync(DeleteHebrewVerses, new { refs = batch }, _transaction, CommandTimeoutSeconds);
        }
    }

    public async Task InsertAsync(bool greek, IReadOnlyList<MappingRowPlan> rows)
    {
        for (var start = 0; start < rows.Count; start += BatchSize)
        {
            var batch = rows.Skip(start).Take(BatchSize).ToList();

            var parameters = new
            {
                ElbWordId = batch.Select(r => r.ElbWordId).ToArray(),
                StepWordId = batch.Select(r => r.StepWordId).ToArray(),
                HebRefId = batch.Select(r => r.HebRefId).ToArray(),
                PositionInVerse = batch.Select(r => r.PositionInVerse).ToArray(),
                IsAddedWord = batch.Select(r => r.Added).ToArray(),
                ParentGermanWordId = batch.Select(r => r.ParentGermanWordId).ToArray(),
                GermanWordPart = batch.Select(r => r.Part).ToArray(),
                PartOrder = batch.Select(r => r.PartOrder).ToArray(),
                Role = batch.Select(r => r.Role).ToArray(),
                PartLemma = batch.Select(r => r.PartLemma).ToArray(),
                MultiHead = batch.Select(r => r.MultiHead).ToArray(),
                StrongsNumber = batch.Select(r => r.Strongs).ToArray()
            };

            await _connection.ExecuteAsync(greek ? InsertGreekRows : InsertHebrewRows, parameters, _transaction, CommandTimeoutSeconds);
        }
    }

    /// <summary>The content hash (see <c>MappingVerseHash</c>) of the given verses as the database holds them right now, inside this transaction.</summary>
    public async Task<Dictionary<int, string>> ReadVerseHashesAsync(int[] hebRefIds)
    {
        var result = new Dictionary<int, string>();

        foreach (var batch in hebRefIds.Chunk(5_000))
        {
            var rows = await _connection.QueryAsync<VerseHashRow>(MappingSeedRepository.VerseHashesSql, new { refs = batch }, _transaction, CommandTimeoutSeconds);

            foreach (var row in rows)
            {
                result[row.HebRefId] = row.ContentHash;
            }
        }

        return result;
    }

    public async Task UpsertStateAsync(string seedVersion, Dictionary<int, string> hashes)
    {
        foreach (var batch in hashes.Chunk(5_000))
        {
            var parameters = new
            {
                kind = MappingSeedRepository.Kind,
                seedVersion,
                refs = batch.Select(p => p.Key).ToArray(),
                hashes = batch.Select(p => p.Value).ToArray()
            };

            await _connection.ExecuteAsync(UpsertState, parameters, _transaction, CommandTimeoutSeconds);
        }
    }

    public async Task DeleteStateAsync(int[] hebRefIds)
    {
        foreach (var batch in hebRefIds.Chunk(5_000))
        {
            await _connection.ExecuteAsync(DeleteState, new { kind = MappingSeedRepository.Kind, refs = batch }, _transaction, CommandTimeoutSeconds);
        }
    }

    public async Task CompleteAsync(MappingImportRecord record)
    {
        var parameters = new
        {
            Kind = MappingSeedRepository.Kind,
            record.SeedVersion,
            record.ExportedAt,
            record.Rows,
            record.Exact,
            record.Shifted,
            record.Modified,
            record.Orphaned,
            record.VersesUpdated,
            record.VersesKeptLocal
        };

        await _connection.ExecuteAsync(InsertSeedImport, parameters, _transaction, CommandTimeoutSeconds);

        _transaction.Commit();
        _completed = true;
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
