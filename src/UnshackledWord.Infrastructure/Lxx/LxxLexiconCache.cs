using Dapper;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Features.AgentMapping.Lxx;
using UnshackledWord.Application.Features.Lxx.Review;

namespace UnshackledWord.Infrastructure.Lxx;

/// <summary>Singleton: the Greek TBESG entries (about 11k rows) loaded on first use and kept for the process (TBESG changes only with a SeedDb re-import).</summary>
public sealed class LxxLexiconCache : ILxxLexiconSource
{
    private readonly IDbConnectionFactory _factory;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private LxxLexicon? _lexicon;

    public LxxLexiconCache(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<LxxLexicon> GetAsync(CancellationToken token = default)
    {
        if (_lexicon is { } ready)
        {
            return ready;
        }

        await _gate.WaitAsync(token);
        try
        {
            if (_lexicon is null)
            {
                using var connection = await _factory.CreateDbConnectionAsync(token);
                var rows = await connection.QueryAsync<(string Strongs, string Lemma, string Gloss)>(new CommandDefinition(LxxAgentSql.Lexicon, cancellationToken: token));
                _lexicon = LxxLexicon.Build(rows.Select(r => new LxxLexiconEntry(r.Strongs, r.Lemma, r.Gloss ?? string.Empty)));
            }

            return _lexicon;
        }
        finally
        {
            _gate.Release();
        }
    }
}
