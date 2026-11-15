using FastEndpoints;
using UnshackledWord.Application.Repositories;
using UnshackledWord.Domain.WebApi.BibleTagger.CreateElbSrMapping;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.BibleTagger.CreateElbSrMapping;

[RequiresLogin]
public sealed class Endpoint : Ep.Req<CreateElbSrRequest>.Res<CreateElbSrResponse>
{
    private readonly IElb1871WordRepository _elbRepo;
    private readonly IElb1871TaggingRepository _elbTaggingRepo;

    public Endpoint(IElb1871WordRepository elbRepo, IElb1871TaggingRepository elbTaggingRepo)
    {
        _elbRepo = elbRepo;
        _elbTaggingRepo = elbTaggingRepo;
    }

    public override void Configure()
    {
        Post("mapping/create");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
    }

    public override async Task<CreateElbSrResponse> ExecuteAsync(CreateElbSrRequest req, CancellationToken ct)
    {
        var result = await _elbTaggingRepo.CreateMappingsAsync(req.Elb1871Word, req.SrGntWord, ct);

        return new()
        {
            InsertedTags = result.InsertedMappingsCount,
            UpdatedElbWordIds = result.UpdatedElbWordIds
        };
    }
}
