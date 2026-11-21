using FastEndpoints;
using UnshackledWord.Domain.WebApi.Vocab;
using UnshackledWord.Infrastructure.Vocab;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Vocab.GetEntry;

/// <summary>Anonymous: one vocab entry with its keys, accepted meanings, German renderings and sample verses. Subject to the general anonymous rate limit.</summary>
public sealed class Endpoint : Endpoint<GetVocabEntryRequest, GetVocabEntryResponse>
{
    private readonly VocabEntryRepository _repository;

    public Endpoint(VocabEntryRepository repository)
    {
        _repository = repository;
    }

    public override void Configure()
    {
        Get("entries/{id}");
        Group<RouteGroupConfig>();
        AllowAnonymous();
        Summary(s => s.Summary = "A vocab entry: its Strong's keys, accepted meanings, German renderings summed over the keys (from the last refresh, with its time) and up to three sample verses. 404 for unknown or retired entries.");
    }

    public override async Task HandleAsync(GetVocabEntryRequest req, CancellationToken ct)
    {
        var response = await _repository.GetAsync(req.Id);

        if (response is null)
        {
            await Send.NotFoundAsync(ct);

            return;
        }

        await Send.OkAsync(response, ct);
    }
}
