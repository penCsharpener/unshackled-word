using UnshackledWord.Application.Features.Auth;

namespace UnshackledWord.Application.Features.UserAdmin;

/// <summary>State read under row lock; "active admin" means admin role AND not deactivated.</summary>
public sealed record GuardSnapshot(
    int ActorId,
    int TargetId,
    bool TargetIsAi,
    bool TargetIsDeactivated,
    bool TargetIsAdmin,
    int ActiveAdminCount);

/// <summary>Error codes of the 409 problem details (the frontend maps them to <c>admin.users.errors.&lt;code&gt;</c>).</summary>
public static class UserAdminErrors
{
    public const string AiUser = "ai_user";
    public const string OwnAdminRole = "own_admin_role";
    public const string LastAdmin = "last_admin";
    public const string Self = "self";
    public const string AlreadyDeactivated = "already_deactivated";
    public const string EmailTaken = "email_taken";
    public const string Deactivated = "deactivated";
    public const string NotDeactivated = "not_deactivated";
}

/// <summary>Pure self-protection rules of the user administration (roadmap 0020, 06-logic.md section 1). Null means allowed.</summary>
public static class UserAdminGuards
{
    /// <summary>Roles an admin may assign; <c>user</c> is implicit and <c>ai</c> is never assignable.</summary>
    public static readonly string[] AssignableRoles = [AppRoles.Admin, AppRoles.Maintainer, AppRoles.Reviewer];

    public static string? CheckSetRoles(GuardSnapshot s, IReadOnlySet<string> newRoles)
    {
        if (s.TargetIsAi)
        {
            return UserAdminErrors.AiUser;
        }

        var losesAdmin = s.TargetIsAdmin && !newRoles.Contains(AppRoles.Admin);

        if (losesAdmin && s.ActorId == s.TargetId)
        {
            return UserAdminErrors.OwnAdminRole;
        }

        if (losesAdmin && !s.TargetIsDeactivated && s.ActiveAdminCount <= 1)
        {
            return UserAdminErrors.LastAdmin;
        }

        return null;
    }

    public static string? CheckDeactivate(GuardSnapshot s)
    {
        if (s.TargetIsAi)
        {
            return UserAdminErrors.AiUser;
        }

        if (s.ActorId == s.TargetId)
        {
            return UserAdminErrors.Self;
        }

        if (s.TargetIsDeactivated)
        {
            return UserAdminErrors.AlreadyDeactivated;
        }

        if (s.TargetIsAdmin && s.ActiveAdminCount <= 1)
        {
            return UserAdminErrors.LastAdmin;
        }

        return null;
    }
}
