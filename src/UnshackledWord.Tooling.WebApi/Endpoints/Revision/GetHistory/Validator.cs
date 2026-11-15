using FastEndpoints;
using FluentValidation;
using UnshackledWord.Domain.WebApi.Revision;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Revision.GetHistory;

public sealed class Validator : Validator<RevisionHistoryRequest>
{
    public Validator()
    {
        RuleFor(x => x.BibleBookId).InclusiveBetween(1, 66);
        RuleFor(x => x.ChapterId).InclusiveBetween(1, 999);
        RuleFor(x => x.VerseId).InclusiveBetween(0, 999);
        RuleFor(x => x.Level).InclusiveBetween(1, 1000);
    }
}
