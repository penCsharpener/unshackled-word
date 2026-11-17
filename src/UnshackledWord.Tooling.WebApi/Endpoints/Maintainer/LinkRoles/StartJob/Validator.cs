using FastEndpoints;
using FluentValidation;
using UnshackledWord.Domain.WebApi.Maintainer;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.LinkRoles.StartJob;

public sealed class Validator : Validator<StartLinkRoleJobRequest>
{
    public Validator()
    {
        RuleFor(x => x.Mode)
            .Must(m => LinkRoleJobModes.All.Contains(m))
            .WithMessage($"mode must be one of: {string.Join(", ", LinkRoleJobModes.All)}.");
    }
}
