using FastEndpoints;
using FluentValidation;
using UnshackledWord.Domain.Models.Vocab;
using UnshackledWord.Domain.WebApi.Vocab;
using UnshackledWord.Infrastructure.Vocab;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Vocab.GetCourseEntries;

/// <summary>Anonymous: the entries of a course in rank order, optionally filtered by q. Subject to the general anonymous rate limit.</summary>
public sealed class Endpoint : Endpoint<GetVocabCourseEntriesRequest, GetVocabCourseEntriesResponse>
{
    private readonly VocabCourseRepository _repository;

    public Endpoint(VocabCourseRepository repository)
    {
        _repository = repository;
    }

    public override void Configure()
    {
        Get("courses/{code}/entries");
        Group<RouteGroupConfig>();
        AllowAnonymous();
        Summary(s => s.Summary = "Entries of a vocab course in rank order (take at most 100). q matches the normalized lemma prefix, the transliteration prefix or a German meaning. root lists one word family (root id, own entry first; subRoots=false leaves the sub-roots out).");
    }

    public override async Task HandleAsync(GetVocabCourseEntriesRequest req, CancellationToken ct)
    {
        var q = req.Q?.Trim();
        var hasQuery = !string.IsNullOrEmpty(q);
        var normalized = hasQuery ? VocabText.Normalize(q) : null;

        // a query that normalizes to nothing (only combining marks) cannot match a lemma; it still may match a transliteration or a meaning
        var result = await _repository.GetEntriesAsync(req.Code, req.Skip, req.Take, hasQuery ? normalized ?? string.Empty : null, hasQuery ? q!.ToLowerInvariant() : null, req.Root, req.SubRoots);

        if (result is null)
        {
            await Send.NotFoundAsync(ct);

            return;
        }

        await Send.OkAsync(result, ct);
    }
}

public sealed class Validator : Validator<GetVocabCourseEntriesRequest>
{
    public Validator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(30);
        RuleFor(x => x.Skip).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Take).InclusiveBetween(1, GetVocabCourseEntriesRequest.MaxTake);
        RuleFor(x => x.Q).MaximumLength(GetVocabCourseEntriesRequest.MaxQueryLength);
        RuleFor(x => x.Root).GreaterThanOrEqualTo(1).When(x => x.Root is not null);
    }
}
