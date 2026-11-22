using FastEndpoints;
using FluentValidation;
using UnshackledWord.Domain.WebApi.Maintainer;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.SeedAdmin.StartJob;

public sealed class Validator : Validator<StartSeedJobRequest>
{
    private static readonly string[] Kinds = [ExportSeedRequest.All, ExportSeedRequest.Morphology, ExportSeedRequest.Mappings, ExportSeedRequest.Revision, ExportSeedRequest.Vocab];

    public Validator()
    {
        RuleFor(x => x.Operation)
            .Must(o => SeedOperations.All.Contains(o))
            .WithMessage($"operation must be one of: {string.Join(", ", SeedOperations.All)}.");
        RuleFor(x => x.Kind)
            .Must(k => k is not null && Kinds.Contains(k.ToLowerInvariant()))
            .WithMessage($"kind must be one of: {string.Join(", ", Kinds)}.");
        RuleFor(x => x.ConfirmBackup)
            .Equal(true)
            .When(x => x.Operation == SeedOperations.Rebase)
            .WithMessage("confirmBackup must be true: a rebase empties the German words, morphology and mappings and imports them again.");
    }
}
