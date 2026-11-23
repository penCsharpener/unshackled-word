using FastEndpoints;
using UnshackledWord.Application.Features.Notes;
using UnshackledWord.Domain.WebApi.Notes;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.Notes;

/// <summary>One validator for the DTO shared by approve, reject, hide and unhide (FastEndpoints allows one per DTO).</summary>
public sealed class NoteReviewActionValidator : Validator<NoteReviewActionRequest>
{
    public NoteReviewActionValidator()
    {
        Include(new NoteReviewActionRequestValidator());
    }
}
