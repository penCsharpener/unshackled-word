using System.Security.Claims;
using FastEndpoints;
using FluentValidation;
using UnshackledWord.Application.Features.Vocab.Session;
using UnshackledWord.Domain.WebApi.Vocab;
using UnshackledWord.Infrastructure.Vocab;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Vocab.UpdateSettings;

/// <summary>Replaces the caller's disabled quiz types (roadmap 0022.9). Login required.</summary>
[RequiresLogin]
public sealed class Endpoint : Endpoint<UpdateVocabSettingsRequest, VocabSettingsResponse>
{
    private readonly VocabSettingsService _service;

    public Endpoint(VocabSettingsService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Put("settings");
        Group<RouteGroupConfig>();
        Summary(s => s.Summary = "Sets `disabledQuizTypes`. Only multiple_choice, reverse_multiple_choice, listening, verse_context; at least one of the two multiple-choice types stays on (400 otherwise). Returns the stored settings.");
    }

    public override async Task HandleAsync(UpdateVocabSettingsRequest req, CancellationToken ct)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await Send.OkAsync(await _service.SetAsync(userId, req.DisabledQuizTypes, ct), ct);
    }
}

public sealed class Validator : Validator<UpdateVocabSettingsRequest>
{
    public Validator()
    {
        RuleFor(x => x.DisabledQuizTypes).NotNull();
        RuleForEach(x => x.DisabledQuizTypes)
            .Must(type => VocabQuizTypes.Toggleable.Contains(type))
            .WithMessage("'{PropertyValue}' is not a quiz type that can be turned off.");
        RuleFor(x => x.DisabledQuizTypes)
            .Must(disabled => VocabQuizTypes.MultipleChoiceTypes.Any(type => !disabled.Contains(type)))
            .When(x => x.DisabledQuizTypes is not null)
            .WithMessage("At least one multiple-choice type (multiple_choice or reverse_multiple_choice) must stay on.");
    }
}
