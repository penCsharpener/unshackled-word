using FastEndpoints;
using UnshackledWord.Application.Repositories;
using UnshackledWord.Domain.WebApi.MetaBible.GetBibleBooks;

namespace UnshackledWord.Tooling.WebApi.Endpoints.MetaBible.GetBibleBooks;

public class Endpoint : Ep.Req<GetBibleBooksRequest>.Res<GetBibleBooksResponse>
{
    private readonly IBibleBookRepository _repo;

    public Endpoint(IBibleBookRepository repo)
    {
        _repo = repo;
    }

    public override void Configure()
    {
        Get("bible-books/language/{LanguageId:int}");
        Group<RouteGroupConfig>();
    }

    public override async Task<GetBibleBooksResponse> ExecuteAsync(GetBibleBooksRequest req, CancellationToken ct)
    {
        var books = await _repo.GetBibleBooksAsync(req.LanguageId, ct);

        return new GetBibleBooksResponse { BibleBooks = books };
    }
}
