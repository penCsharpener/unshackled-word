namespace UnshackledWord.Domain.WebApi.Auth;

// Request/response bodies of /api/admin/invite-codes and /api/admin/invitations. Shapes match lexio's;
// CreateInviteCodeRequest.BoundEmail is the one addition (restricts a shared-style code to a single address).

public sealed class InviteCodeSummary
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;

    /// <summary>Set for an emailed or email-restricted code; null for a shared code.</summary>
    public string? BoundEmail { get; set; }

    public int MaxSeats { get; set; }
    public long SeatsUsed { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public string? Note { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string CreatedByDisplayName { get; set; } = string.Empty;
    public string[] RedeemedByDisplayNames { get; set; } = [];
}

public sealed class ListInviteCodesResponse
{
    public IReadOnlyList<InviteCodeSummary> InviteCodes { get; set; } = [];
}

/// <summary>MaxSeats and ExpiresAt are both required: a unlimited or never-expiring code is open registration with extra steps.</summary>
public sealed class CreateInviteCodeRequest
{
    public int MaxSeats { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public string? Note { get; set; }

    /// <summary>Optional: only this address can redeem the code.</summary>
    public string? BoundEmail { get; set; }
}

public sealed class CreateInviteCodeResponse
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public int MaxSeats { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public string? Note { get; set; }
    public string? BoundEmail { get; set; }
}

public sealed class RevokeInviteCodeRequest
{
    public int Id { get; set; }
}

public sealed class InviteByEmailRequest
{
    public string Email { get; set; } = string.Empty;
    public string? Note { get; set; }
}

public sealed class InviteByEmailResponse
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
}
