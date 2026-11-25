using FastEndpoints;
using FluentValidation;
using UnshackledWord.Application.Features.VerseSimilarity;
using UnshackledWord.Domain.WebApi.Maintainer;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.VerseSimilarity.StartRebuild;

/// <summary>Same bounds as <see cref="VerseSimilarityRebuildService.Validate"/> (they fit the run row columns).</summary>
public sealed class Validator : Validator<StartVerseSimilarityRebuildRequest>
{
    public Validator()
    {
        RuleFor(x => x.StopDfShare).GreaterThan(0).LessThanOrEqualTo(1).When(x => x.StopDfShare is not null)
            .WithMessage("stopDfShare must be greater than 0 and at most 1.");
        RuleFor(x => x.TopK).InclusiveBetween(1, 1000).When(x => x.TopK is not null)
            .WithMessage("topK must be between 1 and 1000.");
        RuleFor(x => x.MinScore).GreaterThanOrEqualTo(0).LessThan(100).When(x => x.MinScore is not null)
            .WithMessage("minScore must be at least 0 and below 100.");
        RuleFor(x => x.PhraseWeight).GreaterThanOrEqualTo(0).LessThan(100).When(x => x.PhraseWeight is not null)
            .WithMessage("phraseWeight must be at least 0 and below 100.");
    }
}
