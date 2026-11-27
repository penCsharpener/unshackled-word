using FastEndpoints;
using FluentValidation;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Infrastructure.Vocab;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.VocabEditor.SetRoot;

/// <summary>Sets the manual root of an entry's main key (roadmap 0030.5): simple word, or prefix keys with a Greek root, or Hebrew root letters (created when missing). The rules are those of the agent run; the row becomes <c>manual</c>, a <c>root</c> change is logged and the content version stays.</summary>
[RequiresLogin]
public sealed class Endpoint : VocabEditEndpoint<SetVocabEntryRootRequest>
{
    private readonly VocabRootEditor _editor;

    public Endpoint(VocabRootEditor editor)
    {
        _editor = editor;
    }

    public override void Configure()
    {
        Put("vocab/entries/{id}/root");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "Sets the manual root of the entry's main Strong's key: simple word, or prefix keys (closed preposition list, in word order) with a Greek root (rootId or rootKey), or Hebrew/Aramaic root letters with an optional verb key and homograph (the root is created when missing). Same rules as the agent run; the row becomes manual, a root change is logged, ContentVersion does not change and learners get no notice. 400 with the broken rule, 404 for an unknown entry or root.");
    }

    public override async Task HandleAsync(SetVocabEntryRootRequest req, CancellationToken ct)
    {
        await SendOutcomeAsync(await _editor.SetEntryRootAsync(req.Id, req, UserId, ct), ct);
    }
}

public sealed class Validator : Validator<SetVocabEntryRootRequest>
{
    public Validator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.PrefixKeys).NotNull().Must(keys => keys.Count <= 6).WithMessage("At most 6 prefixes.");
        RuleForEach(x => x.PrefixKeys).Matches(@"^[Gg]\d{1,5}[A-Za-z]?$").WithMessage("A prefix key must look like G4862.");
        RuleFor(x => x.RootId).GreaterThan(0).When(x => x.RootId is not null);
        RuleFor(x => x.RootKey).Matches(@"^[GgHh]\d{1,5}[A-Za-z]?$").WithMessage("rootKey must look like G3140 or H3789.").When(x => !string.IsNullOrWhiteSpace(x.RootKey));
        RuleFor(x => x.RootLetters).MaximumLength(20).When(x => x.RootLetters is not null);
        RuleFor(x => x.Homograph).InclusiveBetween((short)1, (short)20).When(x => x.Homograph is not null);
        RuleFor(x => x).Must(x => x.RootId is null && x.RootKey is null && string.IsNullOrWhiteSpace(x.RootLetters) && x.PrefixKeys.Count == 0)
            .WithMessage("A simple word has no prefixes and no separate root.")
            .When(x => x.Simple);
        RuleFor(x => x).Must(x => x.RootId is not null || !string.IsNullOrWhiteSpace(x.RootKey) || !string.IsNullOrWhiteSpace(x.RootLetters))
            .WithMessage("Give a root (rootId, rootKey or rootLetters) or mark the word as simple.")
            .When(x => !x.Simple);
    }
}
