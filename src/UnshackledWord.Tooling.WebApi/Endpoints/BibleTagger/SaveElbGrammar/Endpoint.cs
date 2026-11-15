using FastEndpoints;
using UnshackledWord.Application.Repositories;
using UnshackledWord.Domain.WebApi.BibleTagger.SaveElbGrammar;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.BibleTagger.SaveElbGrammar;

[RequiresLogin]
public sealed class Endpoint : Ep.Req<SaveElbGrammarRequest>.Res<SaveElbGrammarResponse>
{
    private readonly IElb1871WordRepository _elbRepo;

    public Endpoint(IElb1871WordRepository elbRepo)
    {
        _elbRepo = elbRepo;
    }

    public override void Configure()
    {
        Post("grammar");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
    }

    public override async Task<SaveElbGrammarResponse> ExecuteAsync(SaveElbGrammarRequest req, CancellationToken ct)
    {
        var results = await _elbRepo.BulkUpdateGrammarAsync(req.ElbWord, ct);

        var response = new SaveElbGrammarResponse { ModifiedElbWordIds = results };

        return response;
    }
}
