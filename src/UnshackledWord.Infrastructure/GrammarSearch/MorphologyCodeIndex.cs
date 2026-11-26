using Dapper;
using Microsoft.Extensions.DependencyInjection;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Features.GrammarSearch;
using UnshackledWord.Domain.Models.Grammar;

namespace UnshackledWord.Infrastructure.GrammarSearch;

/// <summary>
/// In-process list of every distinct Greek morphology code (1,161 in the NT), decoded once with <see cref="GreekMorphologyDecoder"/> (roadmap 0029.1).
/// Loaded on first use and re-read after <see cref="MaxAge"/>, like the source form index: the maintainer can re-import the words.
/// </summary>
public sealed class MorphologyCodeIndex : IMorphologyCodeIndex
{
    public static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(30);

    private const string SelectGreekCodes = """
        SELECT DISTINCT w."Morphology" AS "Code"
        FROM "unshackled-word"."StepGreekWords" w
        WHERE 1=1
            AND w."Morphology" <> ''
        """;

    private sealed record Snapshot(IReadOnlyList<MorphologyCodeEntry> Greek, DateTimeOffset ExpiresAt);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _time;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Snapshot? _snapshot;

    public MorphologyCodeIndex(IServiceScopeFactory scopeFactory, TimeProvider time)
    {
        _scopeFactory = scopeFactory;
        _time = time;
    }

    public async Task<IReadOnlyList<MorphologyCodeEntry>> GetGreekAsync(CancellationToken token = default)
    {
        if (_snapshot is { } current && current.ExpiresAt > _time.GetUtcNow()) return current.Greek;

        await _gate.WaitAsync(token);
        try
        {
            if (_snapshot is { } again && again.ExpiresAt > _time.GetUtcNow()) return again.Greek;

            await using var scope = _scopeFactory.CreateAsyncScope();
            var factory = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
            using var connection = await factory.CreateDbConnectionAsync(token);
            var codes = await connection.QueryAsync<string>(new CommandDefinition(SelectGreekCodes, cancellationToken: token));
            var entries = codes.Order(StringComparer.Ordinal).Select(c => new MorphologyCodeEntry(c, GreekMorphologyDecoder.Decode(c))).ToList();

            // An empty table (words not imported yet) is not kept: the next request asks again.
            _snapshot = entries.Count == 0 ? null : new Snapshot(entries, _time.GetUtcNow() + MaxAge);
            return entries;
        }
        finally
        {
            _gate.Release();
        }
    }
}
