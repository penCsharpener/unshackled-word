using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Application.Features.UserAdmin;

namespace UnshackledWord.Tooling.WebApi.Tests;

public class UserAdminGuardsTests
{
    private static GuardSnapshot Snapshot(
        int actor = 1, int target = 2, bool ai = false, bool deactivated = false, bool admin = false, int activeAdmins = 2) =>
        new(actor, target, ai, deactivated, admin, activeAdmins);

    private static IReadOnlySet<string> Roles(params string[] roles) => roles.ToHashSet();

    [Fact]
    public void SetRoles_AiUser_IsRefused() =>
        UserAdminGuards.CheckSetRoles(Snapshot(ai: true), Roles(AppRoles.Reviewer)).ShouldBe("ai_user");

    [Fact]
    public void SetRoles_OwnAdminRemoval_IsRefused() =>
        UserAdminGuards.CheckSetRoles(Snapshot(actor: 5, target: 5, admin: true), Roles(AppRoles.Maintainer)).ShouldBe("own_admin_role");

    [Fact]
    public void SetRoles_OwnAdminKept_IsAllowed() =>
        UserAdminGuards.CheckSetRoles(Snapshot(actor: 5, target: 5, admin: true), Roles(AppRoles.Admin, AppRoles.Reviewer)).ShouldBeNull();

    [Fact]
    public void SetRoles_LastActiveAdminDemoted_IsRefused() =>
        UserAdminGuards.CheckSetRoles(Snapshot(admin: true, activeAdmins: 1), Roles()).ShouldBe("last_admin");

    [Fact]
    public void SetRoles_AdminDemotedWhileOthersRemain_IsAllowed() =>
        UserAdminGuards.CheckSetRoles(Snapshot(admin: true, activeAdmins: 2), Roles()).ShouldBeNull();

    [Fact]
    public void SetRoles_DeactivatedAdminDemoted_IsAllowedEvenIfCountIsOne() =>
        UserAdminGuards.CheckSetRoles(Snapshot(admin: true, deactivated: true, activeAdmins: 1), Roles()).ShouldBeNull();

    [Fact]
    public void SetRoles_GrantingAdminToAPlainUser_IsAllowed() =>
        UserAdminGuards.CheckSetRoles(Snapshot(activeAdmins: 1), Roles(AppRoles.Admin)).ShouldBeNull();

    [Fact]
    public void SetRoles_OwnAdminRemoval_WinsOverLastAdmin() =>
        UserAdminGuards.CheckSetRoles(Snapshot(actor: 5, target: 5, admin: true, activeAdmins: 1), Roles()).ShouldBe("own_admin_role");

    [Fact]
    public void Deactivate_Cases()
    {
        UserAdminGuards.CheckDeactivate(Snapshot(ai: true)).ShouldBe("ai_user");
        UserAdminGuards.CheckDeactivate(Snapshot(actor: 3, target: 3)).ShouldBe("self");
        UserAdminGuards.CheckDeactivate(Snapshot(deactivated: true)).ShouldBe("already_deactivated");
        UserAdminGuards.CheckDeactivate(Snapshot(admin: true, activeAdmins: 1)).ShouldBe("last_admin");
        UserAdminGuards.CheckDeactivate(Snapshot(admin: true, activeAdmins: 2)).ShouldBeNull();
        UserAdminGuards.CheckDeactivate(Snapshot()).ShouldBeNull();
    }
}
