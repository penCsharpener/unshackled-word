using System.Data;

namespace UnshackledWord.Application.Features.UserAdmin;

/// <summary>The allowed values of <c>AdminLog.Action</c> (check constraint <c>AdminLog_Action_CK</c>, migrations 0035 and 0043).</summary>
public static class AdminLogActions
{
    public const string UserCreated = "user_created";
    public const string UserUpdated = "user_updated";
    public const string RoleGranted = "role_granted";
    public const string RoleRevoked = "role_revoked";
    public const string UserDeactivated = "user_deactivated";
    public const string UserReactivated = "user_reactivated";
    public const string PasswordResetSent = "password_reset_sent";
    public const string WelcomeSent = "welcome_sent";
    public const string InviteCreated = "invite_created";
    public const string InviteRevoked = "invite_revoked";
    public const string InviteEmailed = "invite_emailed";
    public const string NoteApproved = "note_approved";
    public const string NoteRejected = "note_rejected";
    public const string NoteHidden = "note_hidden";
    public const string NoteUnhidden = "note_unhidden";

    public static readonly string[] All =
    [
        UserCreated, UserUpdated, RoleGranted, RoleRevoked, UserDeactivated, UserReactivated,
        PasswordResetSent, WelcomeSent, InviteCreated, InviteRevoked, InviteEmailed,
        NoteApproved, NoteRejected, NoteHidden, NoteUnhidden,
    ];
}

/// <summary>One row to append. <see cref="DetailsJson"/> is action-specific JSON and must never contain tokens or password hashes.</summary>
public sealed record AdminLogEntry(int ActorUserId, string Action, int? TargetUserId = null, int? InviteCodeId = null, string DetailsJson = "{}");

public sealed class AdminLogRecord
{
    public long Id { get; init; }
    public int ActorUserId { get; init; }
    public int? TargetUserId { get; init; }
    public int? InviteCodeId { get; init; }
    public string Action { get; init; } = string.Empty;
    public string DetailsJson { get; init; } = "{}";
    public DateTimeOffset CreatedAt { get; init; }
    public string ActorDisplayName { get; init; } = string.Empty;
    public string? TargetDisplayName { get; init; }

    /// <summary>Rows matching the filter over all pages (window count); only meaningful on a non-empty page.</summary>
    public long Total { get; init; }
}

/// <summary>One page of the log plus the number of matching rows over all pages.</summary>
public sealed record AdminLogPage(IReadOnlyList<AdminLogRecord> Items, int Total, int Page, int PageSize);

/// <summary>Filter for <see cref="IAdminLogRepository.QueryAsync"/>; newest entries first. Page is 1-based.</summary>
public sealed record AdminLogQuery(int? TargetUserId = null, string? Action = null, int Page = 1, int PageSize = 50);

/// <summary>Append-only log of admin actions (table <c>AdminLog</c>, roadmap 0020). There is deliberately no update or delete.</summary>
public interface IAdminLogRepository
{
    /// <summary>Appends a row; pass the transaction of the write it documents so both commit or roll back together.</summary>
    Task AppendAsync(AdminLogEntry entry, IDbTransaction? transaction = null, CancellationToken token = default);

    Task<IReadOnlyList<AdminLogRecord>> QueryAsync(AdminLogQuery query, CancellationToken token = default);

    /// <summary>Like <see cref="QueryAsync"/> but with the total and the effective (clamped) page and page size.</summary>
    Task<AdminLogPage> QueryPageAsync(AdminLogQuery query, CancellationToken token = default);
}
