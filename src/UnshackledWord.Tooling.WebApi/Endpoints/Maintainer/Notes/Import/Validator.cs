using FastEndpoints;
using FluentValidation;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Infrastructure.Notes;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.Notes.Import;

public sealed class Validator : Validator<ImportNotesRequest>
{
    public Validator()
    {
        RuleFor(x => x.FileName)
            .Must(n => NoteFileService.FileNamePattern().IsMatch(n!))
            .When(x => !string.IsNullOrWhiteSpace(x.FileName))
            .WithMessage("fileName must look like notes-YYYYMMDD-HHmmss.json.");
    }
}
