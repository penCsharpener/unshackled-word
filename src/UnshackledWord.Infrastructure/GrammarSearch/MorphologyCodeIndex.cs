using Dapper;
using Microsoft.Extensions.DependencyInjection;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Features.GrammarSearch;
using UnshackledWord.Domain.Models.Grammar;

namespace UnshackledWord.Infrastructure.GrammarSearch;

/// <summary>
/// In-process lists of every distinct Greek morphology code (1,161 in the NT, <see cref="GreekMorphologyDecoder"/>) and Hebrew/Aramaic grammar code (3,081 in the OT, keys of the root segment of <see cref="HebrewMorphologyDecoder"/>), decoded once (roadmap 0029.1, 0029.6).
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

    private const string SelectHebrewCodes = """
        SELECT DISTINCT w."Grammar" AS "Code"
        FROM "unshackled-word"."StepHebrewWords" w
        WHERE 1=1
            AND w."Grammar" <> ''
        """;

    private sealed record Snapshot(IReadOnlyList<MorphologyCodeEntry> Greek, IReadOnlyList<MorphologyCodeEntry> Hebrew, DateTimeOffset ExpiresAt);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _time;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Snapshot? _snapshot;

    public MorphologyCodeIndex(IServiceScopeFactory scopeFactory, TimeProvider time)
    {
        _scopeFactory = scopeFactory;
        _time = time;
    }

    public async Task<IReadOnlyList<MorphologyCodeEntry>> GetGreekAsync(CancellationToken token = default) => (await GetSnapshotAsync(token)).Greek;

    public async Task<IReadOnlyList<MorphologyCodeEntry>> GetHebrewAsync(CancellationToken token = default) => (await GetSnapshotAsync(token)).Hebrew;

    private async Task<Snapshot> GetSnapshotAsync(CancellationToken token)
    {
        if (_snapshot is { } current && current.ExpiresAt > _time.GetUtcNow()) return current;

        await _gate.WaitAsync(token);
        try
        {
            if (_snapshot is { } again && again.ExpiresAt > _time.GetUtcNow()) return again;

            await using var scope = _scopeFactory.CreateAsyncScope();
            var factory = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
            using var connection = await factory.CreateDbConnectionAsync(token);
            var greekCodes = await connection.QueryAsync<string>(new CommandDefinition(SelectGreekCodes, cancellationToken: token));
            var hebrewCodes = await connection.QueryAsync<string>(new CommandDefinition(SelectHebrewCodes, cancellationToken: token));
            var greek = greekCodes.Order(StringComparer.Ordinal).Select(c => new MorphologyCodeEntry(c, GreekMorphologyDecoder.Decode(c))).ToList();
            var hebrew = hebrewCodes.Order(StringComparer.Ordinal).Select(c =>
            {
                var decoded = HebrewMorphologyDecoder.Decode(c);
                return new MorphologyCodeEntry(c, decoded.Root, decoded.IsAramaic);
            }).ToList();

            // An empty table (words not imported yet) is not kept: the next request asks again.
            var snapshot = new Snapshot(greek, hebrew, _time.GetUtcNow() + MaxAge);
            _snapshot = greek.Count == 0 || hebrew.Count == 0 ? null : snapshot;
            return snapshot;
        }
        finally
        {
            _gate.Release();
        }
    }
}
