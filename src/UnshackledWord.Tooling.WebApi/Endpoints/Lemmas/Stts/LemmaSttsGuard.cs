using UnshackledWord.Application.Repositories;
using UnshackledWord.Application.Features.Lemmas;
using UnshackledWord.Domain.WebApi.Lemmas;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Lemmas.Stts;

/// <summary>Checks the STTS part of lemma edits against the table and the stored rows before the write.</summary>
public sealed class LemmaSttsGuard
{
    private readonly ISttsTagRepository _tags;
    private readonly ILemmaRepository _lemmas;

    public LemmaSttsGuard(ISttsTagRepository tags, ILemmaRepository lemmas)
    {
        _tags = tags;
        _lemmas = lemmas;
    }

    /// <summary>One error text per rejected change (with the word id); empty when all changes are fine.</summary>
    public async Task<IReadOnlyList<string>> CheckAsync(IReadOnlyList<LemmaWordChange> changes, CancellationToken ct)
    {
        var withStts = changes.Where(c => c.Stts is not null).ToList();
        if (withStts.Count == 0) return [];

        var catalog = new SttsCatalog(await _tags.GetAllAsync(ct));
        var before = (await _lemmas.GetWordsAsync(withStts.Select(c => c.WordId).ToList(), ct)).ToDictionary(r => r.WordId);
        return [.. withStts
            .Select(c => (c.WordId, Error: SttsRules.Check(c, before.GetValueOrDefault(c.WordId) is { HasMorphology: true } row ? row : null, catalog)))
            .Where(x => x.Error is not null)
            .Select(x => $"Word {x.WordId}: {x.Error}")];
    }
}
