using System.Data;
using Dapper;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Features.Lxx;

namespace UnshackledWord.Infrastructure.Lxx;

/// <summary>
/// Dapper access to <c>LxxVerses</c>/<c>LxxWords</c> (roadmap 0028.3, migration 0050). The import runs in one transaction: the verses in one
/// INSERT … SELECT FROM unnest(arrays) … RETURNING, then the words in batches of <see cref="BatchSize"/> rows.
/// </summary>
public sealed class LxxTextRepository : ILxxTextRepository
{
    /// <summary>Rows per word INSERT statement (about 486k words in all).</summary>
    public const int BatchSize = 100_000;

    private const int CommandTimeout = 900;

    private readonly IDbConnectionFactory _factory;

    public LxxTextRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<bool> IsEmptyAsync(CancellationToken token = default)
    {
        using var connection = await _factory.CreateDbConnectionAsync(token);
        return await IsEmptyAsync(connection, null, token);
    }

    public async Task<int> InsertAsync(IReadOnlyList<LxxVerseText> verses, CancellationToken token = default)
    {
        using var connection = await _factory.CreateDbConnectionAsync(token);
        using var transaction = connection.BeginTransaction();

        var inserted = await InsertAsync(connection, transaction, verses, token);

        transaction.Commit();
        return inserted;
    }

    private static Task<bool> IsEmptyAsync(IDbConnection connection, IDbTransaction? transaction, CancellationToken token)
    {
        const string sql = """
                           SELECT NOT EXISTS (SELECT 1 FROM "unshackled-word"."LxxVerses")
                           """;

        return connection.ExecuteScalarAsync<bool>(new CommandDefinition(sql, null, transaction, cancellationToken: token));
    }

    /// <summary>The insert on a given connection and transaction (public for the database test, which rolls back).</summary>
    public static async Task<int> InsertAsync(IDbConnection connection, IDbTransaction transaction, IReadOnlyList<LxxVerseText> verses,
        CancellationToken token = default)
    {
        // Blocks a second importer until this one commits; the gate below then sees the rows.
        const string lockTable = """
                                 LOCK TABLE "unshackled-word"."LxxVerses" IN SHARE ROW EXCLUSIVE MODE
                                 """;

        await connection.ExecuteAsync(new CommandDefinition(lockTable, null, transaction, cancellationToken: token));
        if (verses.Count == 0 || !await IsEmptyAsync(connection, transaction, token))
        {
            return 0;
        }

        const string insertVerses = """
                                    INSERT INTO "unshackled-word"."LxxVerses" ("BibleBookId", "TextForm", "Chapter", "Verse", "Subverse", "RahlfsRefId", "SortOrder", "SweteRef", "LxxRefId", "HebRefId", "AfterHebRefId", "MapKind")
                                    SELECT u."BibleBookId"
                                         , u."TextForm"
                                         , u."Chapter"
                                         , u."Verse"
                                         , u."Subverse"
                                         , u."RahlfsRefId"
                                         , u."SortOrder"
                                         , u."SweteRef"
                                         , u."LxxRefId"
                                         , u."HebRefId"
                                         , u."AfterHebRefId"
                                         , u."MapKind"
                                    FROM unnest(@BookIds::int4[], @TextForms::varchar[], @Chapters::int4[], @Verses::int4[], @Subverses::int4[], @RahlfsRefIds::int8[],
                                                @SortOrders::int4[], @SweteRefs::varchar[], @LxxRefIds::int4[], @HebRefIds::int4[], @AfterHebRefIds::int4[], @MapKinds::varchar[])
                                        AS u ("BibleBookId", "TextForm", "Chapter", "Verse", "Subverse", "RahlfsRefId", "SortOrder", "SweteRef", "LxxRefId", "HebRefId", "AfterHebRefId", "MapKind")
                                    RETURNING "Id", "SortOrder"
                                    """;

        var rows = verses.Select(v => v.Verse).ToList();
        var ids = (await connection.QueryAsync<(int Id, int SortOrder)>(new CommandDefinition(insertVerses, new
        {
            BookIds = rows.Select(v => v.BibleBookId).ToArray(),
            TextForms = rows.Select(v => v.TextForm).ToArray(),
            Chapters = rows.Select(v => v.Chapter).ToArray(),
            Verses = rows.Select(v => v.Verse).ToArray(),
            Subverses = rows.Select(v => v.Subverse).ToArray(),
            RahlfsRefIds = rows.Select(v => v.RahlfsRefId).ToArray(),
            SortOrders = rows.Select(v => v.SortOrder).ToArray(),
            SweteRefs = rows.Select(v => v.SweteRef).ToArray(),
            LxxRefIds = rows.Select(v => v.LxxRefId).ToArray(),
            HebRefIds = rows.Select(v => v.HebRefId).ToArray(),
            AfterHebRefIds = rows.Select(v => v.AfterHebRefId).ToArray(),
            MapKinds = rows.Select(v => v.MapKind).ToArray(),
        }, transaction, CommandTimeout, cancellationToken: token))).ToDictionary(r => r.SortOrder, r => r.Id);

        foreach (var verse in verses)
        {
            verse.Verse.Id = ids[verse.Verse.SortOrder];
            foreach (var word in verse.Words)
            {
                word.LxxVerseId = verse.Verse.Id;
            }
        }

        const string insertWords = """
                                   INSERT INTO "unshackled-word"."LxxWords" ("LxxVerseId", "Position", "Greek", "GreekNoDiacritics", "SrcHash")
                                   SELECT u."LxxVerseId"
                                        , u."Position"
                                        , u."Greek"
                                        , u."GreekNoDiacritics"
                                        , u."SrcHash"
                                   FROM unnest(@VerseIds::int4[], @Positions::int4[], @Greek::varchar[], @Plain::varchar[], @Hashes::varchar[])
                                       AS u ("LxxVerseId", "Position", "Greek", "GreekNoDiacritics", "SrcHash")
                                   """;

        foreach (var batch in verses.SelectMany(v => v.Words).Chunk(BatchSize))
        {
            token.ThrowIfCancellationRequested();

            await connection.ExecuteAsync(new CommandDefinition(insertWords, new
            {
                VerseIds = batch.Select(w => w.LxxVerseId).ToArray(),
                Positions = batch.Select(w => w.Position).ToArray(),
                Greek = batch.Select(w => w.Greek).ToArray(),
                Plain = batch.Select(w => w.GreekNoDiacritics).ToArray(),
                Hashes = batch.Select(w => w.SrcHash).ToArray(),
            }, transaction, CommandTimeout, cancellationToken: token));
        }

        return rows.Count;
    }
}
