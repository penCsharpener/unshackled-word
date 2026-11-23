using System.Data;
using Dapper;
using UnshackledWord.Application.Features.Notes;
using UnshackledWord.Application.Features.TskCuration;
using UnshackledWord.Domain.Models.BibleStructure;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Domain.WebApi.Tsk;
using UnshackledWord.Infrastructure.TskCuration;

namespace UnshackledWord.Infrastructure.Seed.Tsk;

/// <summary>
/// Reads and applies the TSK curation seed (roadmap 0024.7, docs/roadmaps/0024-tsk-curation/05-seed-diff.md). Every method runs on a given connection
/// and transaction, so the database tests can roll back; <see cref="TskCurationSeedService"/> adds the files.
/// </summary>
public static class TskCurationSeed
{
    public const string Kind = "tsk";

    /// <summary>Counts of the import report.</summary>
    public const string Inserted = "inserted";

    public const string Updated = "updated";
    public const string Unchanged = "unchanged";
    public const string Deleted = "deleted";
    public const string Skipped = "skipped";

    /// <summary>Lists of the import report: the skipped lines with their reason, and the keys where the database was newer than the file.</summary>
    public const string SkippedList = "skippedRows";

    public const string NewerInDatabaseList = "newerInDatabase";

    /// <summary>Every <c>TskCurations</c> row as seed line, sorted by source, target start, target end (single verse first).</summary>
    public static async Task<List<TskCurationSeedLine>> ReadAsync(IDbConnection connection, IDbTransaction? transaction, CancellationToken token = default)
    {
        var rows = await ReadRowsAsync(connection, transaction, token);

        return rows.Select(ToLine).ToList();
    }

    public static (byte[] Bytes, string Sha256) Serialize(IEnumerable<TskCurationSeedLine> lines) => SeedCsv.Serialize<TskCurationSeedLine, TskCurationSeedLineMap>(lines);

    public static List<TskCurationSeedLine> Parse(byte[] bytes) => SeedCsv.Read<TskCurationSeedLine, TskCurationSeedLineMap>(bytes);

    /// <summary><c>1Mo 1,1</c>: the readable source column.</summary>
    public static string FormatSource(int hebRefId)
    {
        var book = hebRefId / 1_000_000;
        var chapter = hebRefId / 1000 % 1000;
        var verse = hebRefId % 1000;

        return $"{GermanBookNames.GetAbbreviation(book)} {chapter},{verse}";
    }

    /// <summary>How many database rows the import with "Exakt wie Datei" would delete: keys in <c>TskCurations</c> that the file does not have.</summary>
    public static async Task<int> CountMirrorDeletesAsync(IDbConnection connection, IDbTransaction? transaction, IReadOnlyList<TskCurationSeedLine> lines,
        CancellationToken token = default)
    {
        var fileKeys = lines.Select(KeyOf).ToHashSet();
        var rows = await ReadRowsAsync(connection, transaction, token);

        return rows.Count(r => !fileKeys.Contains(r.Key));
    }

    /// <summary>
    /// Applies the lines: an existing key takes the file's state when the file's <c>updatedAt</c> is newer; a key only in the file is inserted (an
    /// <c>origin = tsk</c> key without a <c>Tsk</c> row is skipped); a key only in the database is kept, or deleted with <paramref name="mirror"/>.
    /// Word links are validated against <c>Elb1871Words</c> (positions must exist); a quote that differs from today's words is imported as it is and shows
    /// as drifted. Every applied change gets one <c>TskCurationAudit</c> row with <c>Source = import</c> and no user. Rows keep the file's <c>updatedAt</c>.
    /// </summary>
    public static async Task<SeedStepReport> ImportAsync(IDbConnection connection, IDbTransaction transaction, IReadOnlyList<TskCurationSeedLine> lines, bool mirror,
        IProgress<string>? progress, CancellationToken token = default)
    {
        var existing = (await ReadRowsAsync(connection, transaction, token)).ToDictionary(r => r.Key);
        var seen = new HashSet<TskLinkKey>();
        var skipped = new List<string>();
        var newerInDatabase = new List<string>();
        var words = new Dictionary<int, IReadOnlyList<NoteWordRow>>();
        int inserted = 0, updated = 0, unchanged = 0, deleted = 0;

        progress?.Report($"Importing {lines.Count} TSK curation lines{(mirror ? " (exactly as the file)" : string.Empty)}");

        foreach (var line in lines)
        {
            var key = KeyOf(line);
            var label = Label(key);

            if (!seen.Add(key))
            {
                skipped.Add($"{label}: duplicate key in the file");
                continue;
            }

            var (state, updatedAt, problem) = await ValidateAsync(connection, transaction, line, key, words, token);
            if (problem is not null)
            {
                skipped.Add($"{label}: {problem}");
                continue;
            }

            if (existing.TryGetValue(key, out var current))
            {
                if (current.State == state!)
                {
                    unchanged++;
                    continue;
                }

                if (updatedAt <= current.UpdatedAt)
                {
                    unchanged++;
                    newerInDatabase.Add(label);
                    continue;
                }

                await WriteAsync(connection, transaction, key, current.State, state, updatedAt, token);
                updated++;
            }
            else
            {
                await WriteAsync(connection, transaction, key, null, state, updatedAt, token);
                inserted++;
            }
        }

        if (mirror)
        {
            foreach (var row in existing.Values.Where(r => !seen.Contains(r.Key)).OrderBy(r => r.Key.SourceHebRefId).ThenBy(r => r.Key.TargetStartHebRefId)
                         .ThenBy(r => r.Key.TargetEndHebRefId ?? 0))
            {
                await WriteAsync(connection, transaction, row.Key, row.State, null, null, token);
                deleted++;
            }
        }

        progress?.Report($"TSK curations: {inserted} inserted, {updated} updated, {unchanged} unchanged, {deleted} deleted, {skipped.Count} skipped");

        return new SeedStepReport { Kind = Kind, Action = "import" }
            .WithCount(Inserted, inserted)
            .WithCount(Updated, updated)
            .WithCount(Unchanged, unchanged)
            .WithCount(Deleted, deleted)
            .WithCount(Skipped, skipped.Count)
            .AddList(SkippedList, skipped, s => s)
            .AddList(NewerInDatabaseList, newerInDatabase, s => s);
    }

    public static async Task<int> CountRowsAsync(IDbConnection connection, IDbTransaction? transaction, CancellationToken token = default) =>
        await connection.ExecuteScalarAsync<int>(new CommandDefinition("""SELECT count(*)::int4 FROM "unshackled-word"."TskCurations" c""", null, transaction,
            cancellationToken: token));

    private static TskLinkKey KeyOf(TskCurationSeedLine line) => new(line.SourceRef, line.TargetStart, line.TargetEnd);

    private static string Label(TskLinkKey key) =>
        $"{key.SourceHebRefId} → {key.TargetStartHebRefId}{(key.TargetEndHebRefId is { } end ? $"–{end}" : string.Empty)}";

    private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;

    private static async Task<(TskCurationState? State, DateTimeOffset UpdatedAt, string? Problem)> ValidateAsync(IDbConnection connection, IDbTransaction transaction,
        TskCurationSeedLine line, TskLinkKey key, Dictionary<int, IReadOnlyList<NoteWordRow>> words, CancellationToken token)
    {
        if (key.SourceHebRefId <= 0 || key.TargetStartHebRefId <= 0
            || key.TargetEndHebRefId is { } end && (end <= key.TargetStartHebRefId || end / 1000 != key.TargetStartHebRefId / 1000))
        {
            return (null, default, "invalid key");
        }

        if (!TskCurationSeedLine.TryParseTime(line.UpdatedAt, out var updatedAt))
        {
            return (null, default, $"invalid updatedAt '{line.UpdatedAt}'");
        }

        var origin = line.Origin;
        if (origin is not (TskOrigins.Tsk or TskOrigins.Added))
        {
            return (null, default, $"unknown origin '{origin}'");
        }

        var rating = NullIfEmpty(line.Rating);
        if (rating is not null && !TskRatings.All.Contains(rating))
        {
            return (null, default, $"unknown rating '{rating}'");
        }

        var comment = TskCurationService.NormaliseComment(line.Comment);
        if (comment is { Length: > SaveTskCurationRequestValidator.MaxCommentLength })
        {
            return (null, default, $"comment longer than {SaveTskCurationRequestValidator.MaxCommentLength} characters");
        }

        var quote = NullIfEmpty(line.Quote);
        if (line.StartPosition is not null || line.EndPosition is not null || quote is not null)
        {
            if (line.StartPosition is not { } from || line.EndPosition is not { } to || quote is null || from < 1 || from > to)
            {
                return (null, default, "incomplete word link");
            }

            if (quote.Length > NoteQuote.MaxLength)
            {
                return (null, default, $"quote longer than {NoteQuote.MaxLength} characters");
            }

            if (!words.TryGetValue(key.SourceHebRefId, out var verseWords))
            {
                verseWords = await TskCurationRepository.GetElbWordsAsync(connection, transaction, key.SourceHebRefId, token);
                words[key.SourceHebRefId] = verseWords;
            }

            if (verseWords.Count(w => w.PositionInVerse >= from && w.PositionInVerse <= to) != to - from + 1)
            {
                return (null, default, $"the Elb1871 verse has no words at positions {from}..{to}");
            }
        }

        var state = new TskCurationState(origin, rating, comment, line.StartPosition, line.EndPosition, quote);
        if (state.IsDefault)
        {
            return (null, default, "neutral TSK link (nothing curated)");
        }

        if (origin == TskOrigins.Tsk)
        {
            if (!await TskCurationRepository.TskLinkExistsAsync(connection, transaction, key, token))
            {
                return (null, default, "no TSK row with this key");
            }
        }
        else if (!await TskCurationRepository.VerseExistsAsync(connection, transaction, key.SourceHebRefId, token)
                 || !await TskCurationRepository.VerseExistsAsync(connection, transaction, key.TargetStartHebRefId, token)
                 || key.TargetEndHebRefId is { } last && !await TskCurationRepository.VerseExistsAsync(connection, transaction, last, token))
        {
            return (null, default, "verse not in Elb1871");
        }

        return (state, updatedAt, null);
    }

    /// <param name="after">Null deletes the row.</param>
    private static async Task WriteAsync(IDbConnection connection, IDbTransaction transaction, TskLinkKey key, TskCurationState? before, TskCurationState? after,
        DateTimeOffset? updatedAt, CancellationToken token)
    {
        await connection.ExecuteAsync(new CommandDefinition("SELECT pg_advisory_xact_lock(@LockClass, @SourceHebRefId)",
            new { TskCurationRepository.LockClass, key.SourceHebRefId }, transaction, cancellationToken: token));

        if (after is null)
        {
            const string delete = """
                                  DELETE FROM "unshackled-word"."TskCurations" c
                                  WHERE 1=1
                                      AND c."SourceHebRefId" = @SourceHebRefId
                                      AND c."TargetStartHebRefId" = @TargetStartHebRefId
                                      AND COALESCE(c."TargetEndHebRefId", 0) = COALESCE(@TargetEndHebRefId::int4, 0)
                                  """;
            await connection.ExecuteAsync(new CommandDefinition(delete, key, transaction, cancellationToken: token));
        }
        else
        {
            const string upsert = """
                                  INSERT INTO "unshackled-word"."TskCurations"
                                      ("SourceHebRefId", "TargetStartHebRefId", "TargetEndHebRefId", "Origin", "Rating", "Comment", "StartPosition", "EndPosition", "QuotedText", "UpdatedAt", "UpdatedByUserId")
                                  VALUES (@SourceHebRefId, @TargetStartHebRefId, @TargetEndHebRefId, @Origin, @Rating, @Comment, @StartPosition, @EndPosition, @QuotedText, @UpdatedAt, NULL)
                                  ON CONFLICT ("SourceHebRefId", "TargetStartHebRefId", COALESCE("TargetEndHebRefId", 0)) DO UPDATE
                                      SET "Origin" = EXCLUDED."Origin"
                                        , "Rating" = EXCLUDED."Rating"
                                        , "Comment" = EXCLUDED."Comment"
                                        , "StartPosition" = EXCLUDED."StartPosition"
                                        , "EndPosition" = EXCLUDED."EndPosition"
                                        , "QuotedText" = EXCLUDED."QuotedText"
                                        , "UpdatedAt" = EXCLUDED."UpdatedAt"
                                        , "UpdatedByUserId" = NULL
                                  """;
            await connection.ExecuteAsync(new CommandDefinition(upsert, new
            {
                key.SourceHebRefId, key.TargetStartHebRefId, key.TargetEndHebRefId, after.Origin, after.Rating, after.Comment, after.StartPosition, after.EndPosition,
                after.QuotedText, UpdatedAt = updatedAt!.Value.UtcDateTime,
            }, transaction, cancellationToken: token));
        }

        const string audit = """
                             INSERT INTO "unshackled-word"."TskCurationAudit"
                                 ("SourceHebRefId", "TargetStartHebRefId", "TargetEndHebRefId", "Before", "After", "UserId", "At", "Source")
                             VALUES (@SourceHebRefId, @TargetStartHebRefId, @TargetEndHebRefId, CAST(@Before AS jsonb), CAST(@After AS jsonb), NULL, now(), @Source)
                             """;
        await connection.ExecuteAsync(new CommandDefinition(audit, new
        {
            key.SourceHebRefId, key.TargetStartHebRefId, key.TargetEndHebRefId, Before = TskCurationRepository.ToJson(before), After = TskCurationRepository.ToJson(after),
            Source = TskCurationSources.Import,
        }, transaction, cancellationToken: token));
    }

    private static async Task<List<TskCurationRecord>> ReadRowsAsync(IDbConnection connection, IDbTransaction? transaction, CancellationToken token)
    {
        const string sql = """
                           SELECT c."SourceHebRefId"
                                , c."TargetStartHebRefId"
                                , c."TargetEndHebRefId"
                                , c."Origin"
                                , c."Rating"
                                , c."Comment"
                                , c."StartPosition"
                                , c."EndPosition"
                                , c."QuotedText"
                                , c."UpdatedAt"
                                , c."UpdatedByUserId"
                           FROM "unshackled-word"."TskCurations" c
                           ORDER BY c."SourceHebRefId", c."TargetStartHebRefId", COALESCE(c."TargetEndHebRefId", 0)
                           """;

        var rows = await connection.QueryAsync<TskCurationRepository.CurationRow>(new CommandDefinition(sql, null, transaction, cancellationToken: token));

        return rows.Select(r => r.ToRecord()).ToList();
    }

    private static TskCurationSeedLine ToLine(TskCurationRecord record) => new()
    {
        Source = FormatSource(record.Key.SourceHebRefId),
        SourceRef = record.Key.SourceHebRefId,
        TargetStart = record.Key.TargetStartHebRefId,
        TargetEnd = record.Key.TargetEndHebRefId,
        Origin = record.State.Origin,
        Rating = record.State.Rating,
        StartPosition = record.State.StartPosition,
        EndPosition = record.State.EndPosition,
        Quote = record.State.QuotedText,
        Comment = record.State.Comment,
        UpdatedAt = TskCurationSeedLine.FormatTime(record.UpdatedAt),
    };
}
