using FastEndpoints;
using FluentValidation;
using UnshackledWord.Application.Features.VerseMapping;
using UnshackledWord.Domain.WebApi.Elberfelder.GetVerseMapping;
using UnshackledWord.Domain.WebApi.Elberfelder.SaveVerseMapping;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Elberfelder.SaveVerseMapping;

public class Endpoint : Endpoint<SaveVerseMappingRequest, GetVerseMappingResponse>
{
    private readonly IVerseMappingService _service;

    public Endpoint(IVerseMappingService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Put("verseMapping/bookId/{BibleBookId:int}/chapterId/{ChapterId:int}/verseId/{VerseId:int}");
        Group<RouteGroupConfig>();
        // No auth system yet: the write gate is the MappingEditing:Enabled setting (see docs/roadmaps/0003-mapping-editor.md).
        AllowAnonymous();
        Description(b => b.Produces<GetVerseMappingResponse>(200).ProducesProblem(400).ProducesProblem(403).ProducesProblem(404));
    }

    public override async Task HandleAsync(SaveVerseMappingRequest req, CancellationToken ct)
    {
        var result = await _service.SaveAsync(req.BibleBookId, req.ChapterId, req.VerseId, req.Links, ct);

        switch (result.Outcome)
        {
            case SaveVerseMappingOutcome.NotFound:
                await Send.NotFoundAsync(ct);
                return;
            case SaveVerseMappingOutcome.Forbidden:
                AddError(result.Errors[0]);
                await Send.ErrorsAsync(StatusCodes.Status403Forbidden, ct);
                return;
            case SaveVerseMappingOutcome.Invalid:
                foreach (var error in result.Errors)
                {
                    AddError(error);
                }

                await Send.ErrorsAsync(StatusCodes.Status400BadRequest, ct);
                return;
        }

        HttpContext.Response.Headers.CacheControl = "no-store";
        await Send.OkAsync(result.Mapping!, ct);
    }
}

public class SaveVerseMappingRequestValidator : Validator<SaveVerseMappingRequest>
{
    public SaveVerseMappingRequestValidator()
    {
        RuleFor(x => x.BibleBookId).InclusiveBetween(1, 66);
        RuleFor(x => x.ChapterId).GreaterThan(0);
        RuleFor(x => x.VerseId).GreaterThan(0);
        RuleFor(x => x.Links).NotNull().Must(l => l.Count <= 500).WithMessage("Too many links.");
        RuleForEach(x => x.Links).NotNull().Must(l => l is null || l.ElbWordId > 0).WithMessage("elbWordId must be positive.");
    }
}
