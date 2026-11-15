using FastEndpoints;
using FluentValidation;
using UnshackledWord.Domain.WebApi.Revision;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Revision.BulkReplaceSearch;

public sealed class Validator : Validator<BulkReplaceSearchRequest>
{
    public Validator()
    {
        RuleFor(x => x.Word).NotEmpty().Must(w => w.Trim().Length is >= 2 and <= 40)
            .Must(w => w.Trim().All(c => char.IsLetter(c) || c is '-' or '\''))
            .WithMessage("word must be one token of 2-40 letters.");
        RuleFor(x => x.SourceRevision).NotEmpty().MaximumLength(30);
        RuleFor(x => x.TargetRevision).NotEmpty().MaximumLength(30);
    }
}
