using FastEndpoints;
using UnshackledWord.Domain.Models.Grammar;
using UnshackledWord.Domain.WebApi.Lemmas;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Lemmas.GetValueMaps;

[RequiresLogin]
public sealed class Endpoint : EndpointWithoutRequest<ValueMapsResponse>
{
    public override void Configure()
    {
        Get("value-maps");
        Group<RouteGroupConfig>();
        Summary(s => s.Summary = "Abbreviation to German/English label maps for every morphology field of the lemma editor.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await Send.OkAsync(new ValueMapsResponse
        {
            Fields = ElbLemmaValueMaps.All.Select(f => new ValueMapField
            {
                Field = f.Field,
                Values = f.Values.Select(v => new ValueMapEntry { Code = v.Code, LabelDe = v.LabelDe, LabelEn = v.LabelEn }).ToList(),
            }).ToList(),
            Rules = ElbLemmaValueMaps.Rules.Select(r => new ApplicabilityRuleEntry
            {
                PartOfSpeech = r.PartOfSpeech,
                Fields = r.Fields,
                ByVerbForm = r.ByVerbForm?.Select(v => new VerbFormRuleEntry { VerbForm = v.VerbForm, Fields = v.Fields }).ToList(),
            }).ToList(),
        }, ct);
    }
}
