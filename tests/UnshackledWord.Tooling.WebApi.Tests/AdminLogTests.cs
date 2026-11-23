using Dapper;
using UnshackledWord.Application.Features.UserAdmin;
using UnshackledWord.Infrastructure.Repositories;
using UnshackledWord.Tooling.WebApi.Auth;
using Db = UnshackledWord.Tooling.WebApi.Tests.UserAdminDatabaseTests;

namespace UnshackledWord.Tooling.WebApi.Tests;

/// <summary>Roadmap 0020.8: invitation log rows and the paged, named log query (real Postgres, see <see cref="UserAdminDatabaseTests"/>).</summary>
public class AdminLogTests
{
    [Fact]
    public void AllActions_AreDistinct_AndCoverEveryConstant()
    {
        AdminLogActions.All.Length.ShouldBe(15);
        AdminLogActions.All.Distinct().Count().ShouldBe(15);
    }

    private static InviteAdminRepository NewInvites(Db.TestFactory factory) => new(factory, new AdminLogRepository(factory));

    private static async Task<IReadOnlyList<AdminLogRecord>> LogOfInviteAsync(Db.TestFactory factory, int inviteId)
    {
        var all = await new AdminLogRepository(factory).QueryAsync(new AdminLogQuery(PageSize: 200));
        return all.Where(l => l.InviteCodeId == inviteId).ToList();
    }

    [Fact]
    public async Task EveryAction_SatisfiesTheCheckConstraint()
    {
        Assert.SkipWhen(string.IsNullOrWhiteSpace(Db.ConnectionString), $"{Db.EnvVar} is not set.");
        var factory = new Db.TestFactory(Db.ConnectionString!);
        var actor = await Db.CreateUserAsync(factory, ["user", "admin"]);
        var log = new AdminLogRepository(factory);

        foreach (var action in AdminLogActions.All)
        {
            await log.AppendAsync(new AdminLogEntry(actor, action, actor));
        }

        (await log.QueryAsync(new AdminLogQuery(TargetUserId: actor))).Count.ShouldBe(AdminLogActions.All.Length);
    }

    [Fact]
    public async Task CreateInvite_LogsInviteCreated_WithSeatsExpiryAndBoundEmail()
    {
        Assert.SkipWhen(string.IsNullOrWhiteSpace(Db.ConnectionString), $"{Db.EnvVar} is not set.");
        var factory = new Db.TestFactory(Db.ConnectionString!);
        var actor = await Db.CreateUserAsync(factory, ["user", "admin"]);

        var (id, _) = await NewInvites(factory).CreateAsync(5, DateTimeOffset.UtcNow.AddDays(7), actor, "note", null, default);

        var row = (await LogOfInviteAsync(factory, id)).Single();
        row.Action.ShouldBe(AdminLogActions.InviteCreated);
        row.ActorUserId.ShouldBe(actor);
        row.DetailsJson.ShouldContain("\"maxSeats\": 5");
        row.DetailsJson.ShouldContain("\"boundEmail\": null");
    }

    [Fact]
    public async Task RevokeInvite_LogsOnce_IsIdempotent_AndUnknownIdLogsNothing()
    {
        Assert.SkipWhen(string.IsNullOrWhiteSpace(Db.ConnectionString), $"{Db.EnvVar} is not set.");
        var factory = new Db.TestFactory(Db.ConnectionString!);
        var actor = await Db.CreateUserAsync(factory, ["user", "admin"]);
        var invites = NewInvites(factory);
        var (id, _) = await invites.CreateAsync(1, DateTimeOffset.UtcNow.AddDays(1), actor, null, null, default);

        (await invites.RevokeAsync(id, actor, default)).ShouldBeTrue();
        (await invites.RevokeAsync(id, actor, default)).ShouldBeTrue();
        (await invites.RevokeAsync(int.MaxValue, actor, default)).ShouldBeFalse();

        (await LogOfInviteAsync(factory, id)).Select(l => l.Action).ShouldBe([AdminLogActions.InviteRevoked, AdminLogActions.InviteCreated]);
    }

    [Fact]
    public async Task InviteByEmail_ReissueLogsRevokedForTheOldCode_AndCreatedForTheNewOne()
    {
        Assert.SkipWhen(string.IsNullOrWhiteSpace(Db.ConnectionString), $"{Db.EnvVar} is not set.");
        var factory = new Db.TestFactory(Db.ConnectionString!);
        var actor = await Db.CreateUserAsync(factory, ["user", "admin"]);
        var invites = NewInvites(factory);
        var email = $"{Guid.NewGuid():N}@EXAMPLE.ORG";

        var (first, _) = await invites.CreateInvitationAsync(email, DateTimeOffset.UtcNow.AddDays(14), actor, null, default);
        var (second, _) = await invites.CreateInvitationAsync(email, DateTimeOffset.UtcNow.AddDays(14), actor, null, default);

        (await LogOfInviteAsync(factory, first)).Select(l => l.Action).ShouldBe([AdminLogActions.InviteRevoked, AdminLogActions.InviteCreated]);
        (await LogOfInviteAsync(factory, second)).Select(l => l.Action).ShouldBe([AdminLogActions.InviteCreated]);
    }

    [Fact]
    public async Task QueryPage_FiltersByUserAndAction_NewestFirst_WithNamesAndTotal()
    {
        Assert.SkipWhen(string.IsNullOrWhiteSpace(Db.ConnectionString), $"{Db.EnvVar} is not set.");
        var factory = new Db.TestFactory(Db.ConnectionString!);
        var actor = await Db.CreateUserAsync(factory, ["user", "admin"], name: "Actor Anna");
        var target = await Db.CreateUserAsync(factory, ["user"], name: "Target Tom");
        var log = new AdminLogRepository(factory);

        await log.AppendAsync(new AdminLogEntry(actor, AdminLogActions.RoleGranted, target, null, """{"role":"reviewer"}"""));
        await log.AppendAsync(new AdminLogEntry(actor, AdminLogActions.UserDeactivated, target));
        await log.AppendAsync(new AdminLogEntry(actor, AdminLogActions.UserCreated, actor));

        var page = await log.QueryPageAsync(new AdminLogQuery(target, null, 1, 1));
        page.Total.ShouldBe(2);
        page.PageSize.ShouldBe(1);
        page.Items.Single().Action.ShouldBe(AdminLogActions.UserDeactivated);
        page.Items.Single().ActorDisplayName.ShouldBe("Actor Anna");
        page.Items.Single().TargetDisplayName.ShouldBe("Target Tom");

        var second = await log.QueryPageAsync(new AdminLogQuery(target, null, 2, 1));
        second.Items.Single().Action.ShouldBe(AdminLogActions.RoleGranted);

        var beyond = await log.QueryPageAsync(new AdminLogQuery(target, null, 9, 1));
        beyond.Items.ShouldBeEmpty();
        beyond.Total.ShouldBe(2);

        var byAction = await log.QueryPageAsync(new AdminLogQuery(target, AdminLogActions.RoleGranted));
        byAction.Total.ShouldBe(1);
    }
}
