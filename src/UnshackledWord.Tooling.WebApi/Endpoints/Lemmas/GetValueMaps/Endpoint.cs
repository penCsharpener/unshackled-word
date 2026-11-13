using FastEndpoints;
using UnshackledWord.Domain.Models.Grammar;
using UnshackledWord.Domain.WebApi.Lemmas;
using UnshackledWord.Tooling.WebApi.Auth;
using UnshackledWord.Tooling.WebApi.Endpoints.Lemmas.Stts;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Lemmas.GetValueMaps;

[RequiresLogin]
public sealed class Endpoint : EndpointWithoutRequest<ValueMapsResponse>
{
    private readonly ISttsTagRepository _sttsTags;

    public Endpoint(ISttsTagRepository sttsTags)
    {
        _sttsTags = sttsTags;
    }

    public override void Configure()
    {
        Get("value-maps");
        Group<RouteGroupConfig>();
        Summary(s => s.Summary = "Abbreviation to German/English label maps for every morphology field of the lemma editor.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var sttsTags = await _sttsTags.GetAllAsync(ct);
        await Send.OkAsync(new ValueMapsResponse
        {
            // The STTS part comes from the SttsTags table (source of truth); the in-code list is the fallback while the table is not seeded.
            Fields = ElbLemmaValueMaps.All.Select(f => new ValueMapField
            {
                Field = f.Field,
                Values = (f.Field == ElbLemmaValueMaps.Stts && sttsTags.Count > 0
                    ? sttsTags.Select(t => new ValueMapEntry { Code = t.Tag, LabelDe = t.LabelDe, LabelEn = t.LabelEn })
                    : f.Values.Select(v => new ValueMapEntry { Code = v.Code, LabelDe = v.LabelDe, LabelEn = v.LabelEn })).ToList(),
            }).ToList(),
            SttsTags = sttsTags.Select(t => new SttsTagEntry { Tag = t.Tag, Upos = t.Upos, AllowedUpos = t.AllowedUpos }).ToList(),
            SttsProposals = SttsProposal.Rules.Select(r => new SttsProposalEntry { PartOfSpeech = r.PartOfSpeech, VerbForm = r.VerbForm, Mood = r.Mood, Stts = r.Stts }).ToList(),
            Rules = ElbLemmaValueMaps.Rules.Select(r => new ApplicabilityRuleEntry
            {
                PartOfSpeech = r.PartOfSpeech,
                Fields = r.Fields,
                ByVerbForm = r.ByVerbForm?.Select(v => new VerbFormRuleEntry { VerbForm = v.VerbForm, Fields = v.Fields }).ToList(),
            }).ToList(),
        }, ct);
    }
}
