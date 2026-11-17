using UnshackledWord.Application.Features.AgentMapping;
using UnshackledWord.Domain.WebApi.Lemmas;
using UnshackledWord.Tooling.WebApi.Endpoints.Lemmas;
using UnshackledWord.Tooling.WebApi.Endpoints.Lemmas.Stts;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.AgentReview;

/// <summary>Lets the proposal merge (Application) use the lemma editor's repository and STTS guard, which live in this project.</summary>
public sealed class AgentLemmaWriter : IAgentLemmaWriter
{
    private readonly ILemmaRepository _repository;
    private readonly LemmaSttsGuard _guard;

    public AgentLemmaWriter(ILemmaRepository repository, LemmaSttsGuard guard)
    {
        _repository = repository;
        _guard = guard;
    }

    public Task<IReadOnlyList<LemmaWordRow>> GetVerseAsync(int bibleBookId, int chapter, int verse, CancellationToken token = default) =>
        _repository.GetVerseAsync(bibleBookId, chapter, verse, token);

    public Task<IReadOnlyList<string>> CheckAsync(IReadOnlyList<LemmaWordChange> changes, CancellationToken token = default) =>
        _guard.CheckAsync(changes, token);

    public Task<IReadOnlyList<int>> ApplyAsync(IReadOnlyList<LemmaWordChange> changes, int authorUserId, int reviewerUserId, CancellationToken token = default) =>
        _repository.ApplyAsync(changes, authorUserId, token, reviewerUserId);
}
