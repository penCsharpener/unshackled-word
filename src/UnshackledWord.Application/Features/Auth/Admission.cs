using Microsoft.Extensions.Options;

namespace UnshackledWord.Application.Features.Auth;

/// <summary>The <c>Registration:Mode</c> setting. Explicit, not inferred from the allowlist being empty.</summary>
public enum RegistrationMode
{
    /// <summary>Anyone may register.</summary>
    Open,

    /// <summary>An address may only register if it passes the config filter or carries a valid invite code.</summary>
    Restricted,
}

/// <summary>Bound from the "Registration" section of appsettings.json.</summary>
public sealed class RegistrationOptions
{
    public RegistrationMode Mode { get; set; } = RegistrationMode.Open;

    /// <summary>Domains admitted without invite code; whole domain label, never a suffix.</summary>
    public string[] AllowedEmailDomains { get; set; } = [];

    /// <summary>Individual addresses admitted without invite code.</summary>
    public string[] AllowedEmails { get; set; } = [];
}

/// <summary>Pure config-driven allowlist check. <paramref name="emailNormalized"/> must already be normalized.</summary>
public static class EmailFilter
{
    public static bool IsAllowed(string emailNormalized, RegistrationOptions options)
    {
        if (options.AllowedEmails.Any(e => EmailNormalizer.Normalize(e) == emailNormalized))
        {
            return true;
        }

        var atIndex = emailNormalized.LastIndexOf('@');
        if (atIndex < 0)
        {
            return false;
        }

        var domain = emailNormalized[(atIndex + 1)..];
        return options.AllowedEmailDomains.Any(d => EmailNormalizer.Normalize(d) == domain);
    }
}

/// <summary>What the admission gate decided. ViaInviteCode tells Register whether to spend a seat on the code.</summary>
public sealed class AdmissionOutcome
{
    private AdmissionOutcome(bool isAdmitted, bool viaInviteCode, string? refusalMessage, bool refusalAttributedToInviteCode)
    {
        IsAdmitted = isAdmitted;
        ViaInviteCode = viaInviteCode;
        RefusalMessage = refusalMessage;
        RefusalAttributedToInviteCode = refusalAttributedToInviteCode;
    }

    public bool IsAdmitted { get; }
    public bool ViaInviteCode { get; }
    public string? RefusalMessage { get; }
    public bool RefusalAttributedToInviteCode { get; }

    public static AdmissionOutcome Admitted(bool viaInviteCode) => new(true, viaInviteCode, null, false);

    public static AdmissionOutcome Refused(string message, bool attributedToInviteCode) =>
        new(false, false, message, attributedToInviteCode);
}

/// <summary>What the repository reads back for an invite code, enough to name which rule it fails.</summary>
public sealed class InviteCodeCheckRow
{
    public int Id { get; init; }
    public string? BoundEmailNormalized { get; init; }
    public int MaxSeats { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
    public DateTimeOffset? RevokedAt { get; init; }
    public long SeatsUsed { get; init; }
}

public interface IAdmissionRepository
{
    Task<long> CountUsersAsync(CancellationToken ct);

    Task<InviteCodeCheckRow?> FindInviteCodeAsync(string code, CancellationToken ct);
}

/// <summary>The one place that decides whether an address may create an account.</summary>
public sealed class AdmissionCheck
{
    private readonly RegistrationOptions _options;
    private readonly IAdmissionRepository _repository;
    private readonly TimeProvider _time;

    public AdmissionCheck(IOptions<RegistrationOptions> options, IAdmissionRepository repository, TimeProvider time)
    {
        _options = options.Value;
        _repository = repository;
        _time = time;
    }

    public async Task<AdmissionOutcome> CheckAsync(string emailNormalized, string? inviteCodeNormalized, CancellationToken ct)
    {
        if (_options.Mode == RegistrationMode.Open)
        {
            return AdmissionOutcome.Admitted(viaInviteCode: false);
        }

        // The gate closes behind the first account: an empty database has no admin yet to issue invitations.
        if (await _repository.CountUsersAsync(ct) == 0)
        {
            return AdmissionOutcome.Admitted(viaInviteCode: false);
        }

        if (EmailFilter.IsAllowed(emailNormalized, _options))
        {
            return AdmissionOutcome.Admitted(viaInviteCode: false);
        }

        if (string.IsNullOrEmpty(inviteCodeNormalized))
        {
            return AdmissionOutcome.Refused("Registration is by invitation only.", attributedToInviteCode: false);
        }

        var code = await _repository.FindInviteCodeAsync(inviteCodeNormalized, ct);

        if (code is null)
        {
            return AdmissionOutcome.Refused("This invite code is not valid.", attributedToInviteCode: true);
        }

        if (code.RevokedAt is not null)
        {
            return AdmissionOutcome.Refused("This invite code has been revoked.", attributedToInviteCode: true);
        }

        if (code.ExpiresAt <= _time.GetUtcNow())
        {
            return AdmissionOutcome.Refused("This invite code has expired.", attributedToInviteCode: true);
        }

        if (code.SeatsUsed >= code.MaxSeats)
        {
            return AdmissionOutcome.Refused("This invite code has already been used up.", attributedToInviteCode: true);
        }

        if (code.BoundEmailNormalized is not null && code.BoundEmailNormalized != emailNormalized)
        {
            return AdmissionOutcome.Refused("This invite code was issued for a different email address.", attributedToInviteCode: true);
        }

        return AdmissionOutcome.Admitted(viaInviteCode: true);
    }
}
