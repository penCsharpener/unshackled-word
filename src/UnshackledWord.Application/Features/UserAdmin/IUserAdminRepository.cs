using UnshackledWord.Domain.WebApi.UserAdmin;

namespace UnshackledWord.Application.Features.UserAdmin;

/// <summary>Filter of the admin user list. Search is plain text (wildcards are escaped by the repository). Page is 1-based.</summary>
public sealed record UserListQuery(string? Search, string? Role, string? Status, int Page = 1, int PageSize = 50);

/// <param name="NotFound">The target user does not exist.</param>
/// <param name="Error">A guard code from <see cref="UserAdminErrors"/>; nothing was changed.</param>
public sealed record SetRolesOutcome(bool NotFound, string? Error, UserDetail? Detail);

/// <param name="Error">A guard code from <see cref="UserAdminErrors"/> (<c>ai_user</c>, <c>self</c>, <c>last_admin</c>, <c>already_deactivated</c>, <c>not_deactivated</c>).</param>
public sealed record DeactivationOutcome(bool NotFound, string? Error, UserDetail? Detail);

/// <param name="ExistingUserId">Set when the email already belongs to another account (409 <c>email_taken</c>); nothing was written.</param>
public sealed record CreateUserOutcome(int? ExistingUserId, UserDetail? Detail);

/// <param name="Error">A guard code from <see cref="UserAdminErrors"/> (<c>ai_user</c>, <c>email_taken</c>).</param>
/// <param name="ExistingUserId">The other account when <paramref name="Error"/> is <c>email_taken</c>.</param>
/// <param name="PreviousEmail">The old address when the email changed (notice recipient); null otherwise.</param>
public sealed record UpdateUserOutcome(bool NotFound, string? Error, int? ExistingUserId, UserDetail? Detail, string? PreviousEmail);

/// <summary>What the password-reset endpoint needs to decide between reset and welcome mail. Deactivation is asked of <c>IAuthRepository.IsDeactivatedAsync</c>.</summary>
public sealed record ResetTarget(int Id, string Email, string DisplayName, bool HasPassword, bool IsAi);

public interface IUserAdminRepository
{
    /// <summary>
    /// Creates a verified account without password with roles <c>user</c> plus the given assignable ones, and the <c>user_created</c> log row,
    /// in one transaction. The welcome token and mail are the caller's job.
    /// </summary>
    Task<CreateUserOutcome> CreateAsync(int actorId, string email, string displayName, IReadOnlyCollection<string> roles, CancellationToken token = default);

    /// <summary>
    /// Changes display name and/or email in one transaction. An email change marks it verified, rotates the stamp, ends open emailed links and logs
    /// <c>user_updated</c> with the changed keys only. Nothing changed means no log row.
    /// </summary>
    Task<UpdateUserOutcome> UpdateAsync(int actorId, int targetId, string displayName, string email, CancellationToken token = default);

    Task<ResetTarget?> FindResetTargetAsync(int id, CancellationToken token = default);

    Task<UserListResponse> ListAsync(UserListQuery query, CancellationToken token = default);

    /// <summary>Null when the user does not exist. <paramref name="actorId"/> only feeds <c>IsSelf</c>.</summary>
    Task<UserDetail?> GetDetailAsync(int id, int actorId, CancellationToken token = default);

    /// <summary>
    /// Sets the assignable roles (admin, maintainer, reviewer) of a user in one transaction: lock, guard, write, stamp rotation,
    /// one AdminLog row per granted and per revoked role. No change means no log row and no stamp rotation.
    /// </summary>
    Task<SetRolesOutcome> SetRolesAsync(int actorId, int targetId, IReadOnlyCollection<string> roles, CancellationToken token = default);

    /// <summary>
    /// Deactivates an account in one transaction: lock, guard (ai, self, already deactivated, last active admin), mark, rotate the stamp,
    /// consume every open emailed token, log <c>user_deactivated</c>.
    /// </summary>
    Task<DeactivationOutcome> DeactivateAsync(int actorId, int targetId, string? reason, CancellationToken token = default);

    /// <summary>Clears the deactivation (<c>not_deactivated</c> when there was none) and logs <c>user_reactivated</c> with the previous time and reason.</summary>
    Task<DeactivationOutcome> ReactivateAsync(int actorId, int targetId, CancellationToken token = default);
}
