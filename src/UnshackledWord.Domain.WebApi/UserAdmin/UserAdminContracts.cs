namespace UnshackledWord.Domain.WebApi.UserAdmin;

// Request/response bodies of /api/admin/users (roadmap 0020, 05-api.md).

public static class UserStatuses
{
    public const string Active = "active";
    public const string Deactivated = "deactivated";
    public const string Ai = "ai";

    public static readonly string[] All = [Active, Deactivated, Ai];
}

public sealed record UserListItem(
    int Id,
    string Email,
    string DisplayName,
    string[] Roles,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastSignInAt);

public sealed record UserListResponse(UserListItem[] Items, int Total, int Page, int PageSize);

public sealed record DeactivationInfo(DateTimeOffset At, int ByUserId, string ByDisplayName, string? Reason);

/// <summary><c>IsSelf</c> and <c>IsLastActiveAdmin</c> only drive disabled controls in the UI; the backend guards are authoritative.</summary>
public sealed record UserDetail(
    int Id,
    string Email,
    string DisplayName,
    string[] Roles,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastSignInAt,
    bool EmailVerified,
    bool HasPassword,
    int PasskeyCount,
    DeactivationInfo? Deactivation,
    bool IsSelf,
    bool IsLastActiveAdmin);

public sealed class ListUsersRequest
{
    public string? Search { get; set; }
    public string? Role { get; set; }
    public string? Status { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public sealed class GetUserRequest
{
    public int Id { get; set; }
}

/// <summary>Assignable roles only (admin, maintainer, reviewer); <c>user</c> is implicit.</summary>
public sealed class SetRolesRequest
{
    public int Id { get; set; }
    public string[] Roles { get; set; } = [];
}

public sealed class CreateUserRequest
{
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string[] Roles { get; set; } = [];
}

public sealed class UpdateUserRequest
{
    public int Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public sealed class SendPasswordResetRequest
{
    public int Id { get; set; }
}

public sealed class DeactivateRequest
{
    public int Id { get; set; }
    public string? Reason { get; set; }
}

public sealed class ListAdminLogRequest
{
    public int? UserId { get; set; }
    public string? Action { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public sealed record AdminLogItem(
    long Id,
    DateTimeOffset CreatedAt,
    int ActorUserId,
    string ActorDisplayName,
    string Action,
    int? TargetUserId,
    string? TargetDisplayName,
    int? InviteCodeId,
    System.Text.Json.JsonElement Details);

public sealed record AdminLogResponse(AdminLogItem[] Items, int Total, int Page, int PageSize);
