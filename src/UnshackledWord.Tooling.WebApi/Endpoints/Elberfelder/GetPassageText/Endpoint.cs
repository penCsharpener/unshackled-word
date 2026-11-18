using FastEndpoints;
using FluentValidation;
using UnshackledWord.Application.Repositories;
using UnshackledWord.Domain.WebApi.Elberfelder.GetPassageText;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Elberfelder.GetPassageText;

public class Endpoint : Ep.Req<GetPassageTextRequest>.Res<GetPassageTextResponse>
{
    private readonly IElb1871VerseRepository _verses;

    public Endpoint(IElb1871VerseRepository verses)
    {
        _verses = verses;
    }

    public override void Configure()
    {
        Get("passageText");
        AllowAnonymous();
    }

    public override async Task<GetPassageTextResponse> ExecuteAsync(GetPassageTextRequest req, CancellationToken ct)
    {
        return await PassageTextQuery.ReadAsync(_verses, req.Spans, req.Level, ct);
    }
}

public class GetPassageTextValidator : Validator<GetPassageTextRequest>
{
    public GetPassageTextValidator()
    {
        RuleFor(x => x.Spans)
            .Must(PassageSpanParser.IsValid)
            .WithMessage("Spans must be verse ids or id ranges of existing verses, e.g. 66002001-66002005,66002007.");

        RuleFor(x => x.Level).InclusiveBetween(0, 1000);
    }
}
