using FluentValidation;

namespace UnshackledWord.Infrastructure.Observability;

/// <summary>Section <c>Telemetry</c> (roadmap 0021): OpenTelemetry export to the monitoring stack. Off by default.</summary>
public sealed class TelemetryOptions
{
    public const string SectionName = "Telemetry";

    public bool Enabled { get; set; }

    public string OtlpEndpoint { get; set; } = "http://unshackledword.monitoring:4317";

    public double TraceSampleRatio { get; set; } = 1.0;
}

public sealed class TelemetryOptionsValidator : AbstractValidator<TelemetryOptions>
{
    public TelemetryOptionsValidator()
    {
        When(x => x.Enabled, () =>
        {
            RuleFor(x => x.OtlpEndpoint)
                .Must(v => Uri.TryCreate(v, UriKind.Absolute, out _))
                .WithMessage("Telemetry:OtlpEndpoint must be an absolute URI.");
            RuleFor(x => x.TraceSampleRatio)
                .Must(v => v is > 0 and <= 1)
                .WithMessage("Telemetry:TraceSampleRatio must be greater than 0 and at most 1.");
        });
    }
}
