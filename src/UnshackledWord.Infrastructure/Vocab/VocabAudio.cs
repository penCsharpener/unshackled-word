using System.Security.Cryptography;
using Dapper;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.WebApi.Maintainer;

namespace UnshackledWord.Infrastructure.Vocab;

/// <summary>Pure rules of vocab pronunciation audio (roadmap 0022.6): format check, size limit and object key scheme.</summary>
public static class VocabAudio
{
    public const int MaxBytes = 1024 * 1024;
    public const string ContentType = "audio/mpeg";

    /// <summary>Whether the leading bytes look like MP3: an <c>ID3</c> tag or an MPEG audio frame sync (11 set bits).</summary>
    public static bool LooksLikeMp3(ReadOnlySpan<byte> head) =>
        head.Length >= 3 && head[0] == (byte)'I' && head[1] == (byte)'D' && head[2] == (byte)'3'
        || head.Length >= 2 && head[0] == 0xFF && (head[1] & 0xE0) == 0xE0;

    /// <summary><c>vocab/{track}/{entryId}-{shortHash}.mp3</c>; the hash is the first 8 hex digits of the SHA-256 of the content, so a new upload gets a new key.</summary>
    public static string KeyFor(string track, int entryId, ReadOnlySpan<byte> content)
    {
        var hash = Convert.ToHexString(SHA256.HashData(content))[..8].ToLowerInvariant();

        return $"vocab/{track.ToLowerInvariant()}/{entryId}-{hash}.mp3";
    }

    /// <summary>Presigned read URL, or null without a key or without configured storage.</summary>
    public static string? UrlFor(IObjectStorage? storage, string? key) =>
        storage is null || string.IsNullOrEmpty(key) ? null : storage.GetReadUrl(key);
}

/// <summary>Upload and removal of an entry's pronunciation. Audio never bumps <c>ContentVersion</c>; it sets <c>UpdatedAt</c>/<c>UpdatedByUserId</c> only.</summary>
public sealed class VocabAudioService
{
    private const string SelectEntry = """
        SELECT e."Track"::text AS "Track"
             , e."AudioObjectKey"
             , e."ContentVersion"
        FROM "unshackled-word"."VocabEntries" e
        WHERE e."Id" = @Id
        FOR UPDATE
        """;

    private const string UpdateKey = """
        UPDATE "unshackled-word"."VocabEntries"
        SET "AudioObjectKey" = @Key
          , "UpdatedAt" = now()
          , "UpdatedByUserId" = @UserId
        WHERE "Id" = @Id
        """;

    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IObjectStorage _storage;

    public VocabAudioService(IDbConnectionFactory connectionFactory, IObjectStorage storage)
    {
        _connectionFactory = connectionFactory;
        _storage = storage;
    }

    /// <summary>Stores <paramref name="content"/> under a new key, points the row at it, then deletes the old object.</summary>
    public async Task<VocabEditOutcome> UploadAsync(int id, byte[] content, int userId, CancellationToken token)
    {
        if (content.Length == 0)
        {
            return VocabEditOutcome.Invalid("The file is empty.");
        }

        if (content.Length > VocabAudio.MaxBytes)
        {
            return VocabEditOutcome.Invalid($"The file is larger than {VocabAudio.MaxBytes / 1024} KB.");
        }

        if (!VocabAudio.LooksLikeMp3(content))
        {
            return VocabEditOutcome.Invalid("The file is not an MP3 (no ID3 tag or MPEG frame sync).");
        }

        using var connection = await _connectionFactory.CreateDbConnectionAsync(token);
        using var transaction = connection.BeginTransaction();
        var entry = await connection.QuerySingleOrDefaultAsync<EntryRow>(SelectEntry, new { Id = id }, transaction);

        if (entry is null)
        {
            return VocabEditOutcome.NotFound("The entry does not exist.");
        }

        var key = VocabAudio.KeyFor(entry.Track, id, content);
        using (var stream = new MemoryStream(content, writable: false))
        {
            await _storage.PutAsync(key, stream, VocabAudio.ContentType, token);
        }

        try
        {
            await connection.ExecuteAsync(UpdateKey, new { Id = id, Key = key, UserId = userId }, transaction);
            transaction.Commit();
        }
        catch
        {
            if (key != entry.AudioObjectKey)
            {
                await TryDeleteAsync(key);
            }

            throw;
        }

        if (entry.AudioObjectKey is not null && entry.AudioObjectKey != key)
        {
            await TryDeleteAsync(entry.AudioObjectKey);
        }

        return VocabEditOutcome.Ok(new VocabEditResponse { EntryId = id, ContentVersion = entry.ContentVersion });
    }

    /// <summary>Clears the key, then deletes the object. Without audio this is a no-op that still answers OK.</summary>
    public async Task<VocabEditOutcome> DeleteAsync(int id, int userId, CancellationToken token)
    {
        using var connection = await _connectionFactory.CreateDbConnectionAsync(token);
        using var transaction = connection.BeginTransaction();
        var entry = await connection.QuerySingleOrDefaultAsync<EntryRow>(SelectEntry, new { Id = id }, transaction);

        if (entry is null)
        {
            return VocabEditOutcome.NotFound("The entry does not exist.");
        }

        if (entry.AudioObjectKey is not null)
        {
            await connection.ExecuteAsync(UpdateKey, new { Id = id, Key = (string?)null, UserId = userId }, transaction);
        }

        transaction.Commit();

        if (entry.AudioObjectKey is not null)
        {
            await TryDeleteAsync(entry.AudioObjectKey);
        }

        return VocabEditOutcome.Ok(new VocabEditResponse { EntryId = id, ContentVersion = entry.ContentVersion });
    }

    /// <summary>The row is already consistent; an orphaned object is harmless, so a failed delete must not fail the request.</summary>
    private async Task TryDeleteAsync(string key)
    {
        try
        {
            await _storage.DeleteAsync(key);
        }
        catch (Exception)
        {
            // best effort
        }
    }

    private sealed class EntryRow
    {
        public string Track { get; init; } = string.Empty;
        public string? AudioObjectKey { get; init; }
        public int ContentVersion { get; init; }
    }
}
