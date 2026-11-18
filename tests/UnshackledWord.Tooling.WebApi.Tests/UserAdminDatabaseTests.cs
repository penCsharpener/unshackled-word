using System.Data;
using Dapper;
using Npgsql;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Application.Features.UserAdmin;
using UnshackledWord.Infrastructure.Repositories;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Tests;

/// <summary>
/// Roadmap 0020.2 against a real Postgres (migrations up to 0035). Set <c>UNSHACKLEDWORD_TEST_POSTGRES</c> to a COPY of the dev database,
/// otherwise the tests are skipped. AdminLog is append-only, so the test rows stay behind in that copy.
/// </summary>
public class UserAdminDatabaseTests
{
    internal const string EnvVar = "UNSHACKLEDWORD_TEST_POSTGRES";

    internal static string? ConnectionString => Environment.GetEnvironmentVariable(EnvVar);

    internal sealed class TestFactory(string connectionString) : IDbConnectionFactory
    {
        public IDbConnection DbConnection { get; set; } = new NpgsqlConnection(connectionString);

        public Task BulkInsertAsync<T>(string tableName, string[] columns, ICollection<T> dataList, Action<IBinaryImporter, T> mapping, CancellationToken token = default) =>
            throw new NotSupportedException();

        public IDbConnection CreateDbConnection() => new NpgsqlConnection(connectionString);

        public async Task<IDbConnection> CreateDbConnectionAsync(CancellationToken token = default)
        {
            var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(token);
            return connection;
        }

        public void Dispose() => DbConnection.Dispose();

        public ValueTask DisposeAsync()
        {
            DbConnection.Dispose();
            return ValueTask.CompletedTask;
        }
    }

    private static async Task<int> AnyUserIdAsync(TestFactory factory)
    {
        using var connection = await factory.CreateDbConnectionAsync();
        return await connection.ExecuteScalarAsync<int>("""SELECT min("Id") FROM "unshackled-word"."Users" """);
    }

    [Fact]
    public async Task AdminLog_AppendsAndQueriesNewestFirst_AndRefusesUpdate()
    {
        Assert.SkipWhen(string.IsNullOrWhiteSpace(ConnectionString), $"{EnvVar} is not set.");
        var factory = new TestFactory(ConnectionString!);
        var repository = new AdminLogRepository(factory);
        var userId = await AnyUserIdAsync(factory);

        await repository.AppendAsync(new AdminLogEntry(userId, AdminLogActions.RoleGranted, userId, null, """{"role":"reviewer"}"""));
        await repository.AppendAsync(new AdminLogEntry(userId, AdminLogActions.RoleRevoked, userId, null, """{"role":"reviewer"}"""));

        var rows = await repository.QueryAsync(new AdminLogQuery(TargetUserId: userId, PageSize: 2));

        rows.Count.ShouldBe(2);
        rows[0].Action.ShouldBe(AdminLogActions.RoleRevoked);
        rows[0].DetailsJson.ShouldContain("reviewer");
        rows[0].Id.ShouldBeGreaterThan(rows[1].Id);

        using var connection = await factory.CreateDbConnectionAsync();
        var ex = await Should.ThrowAsync<PostgresException>(() =>
            connection.ExecuteAsync("""UPDATE "unshackled-word"."AdminLog" SET "Action" = 'welcome_sent' """));
        ex.MessageText.ShouldContain("append-only");
    }

    [Fact]
    public async Task IssueToken_SupersedeOpen_LeavesOnlyTheNewestLinkWorking()
    {
        Assert.SkipWhen(string.IsNullOrWhiteSpace(ConnectionString), $"{EnvVar} is not set.");
        var factory = new TestFactory(ConnectionString!);
        var repository = new AuthRepository(factory);
        var userId = await AnyUserIdAsync(factory);

        var first = await repository.IssueTokenAsync(userId, AuthTokenPurpose.PasswordReset, TimeSpan.FromHours(1), supersedeOpen: true, default);
        var second = await repository.IssueTokenAsync(userId, AuthTokenPurpose.PasswordReset, TimeSpan.FromHours(1), supersedeOpen: true, default);

        using var connection = await factory.CreateDbConnectionAsync();
        var open = (await connection.QueryAsync<string>(
            """
            SELECT "TokenHash"
            FROM "unshackled-word"."AuthTokens"
            WHERE "UserId" = @userId AND "Purpose" = @purpose AND "ConsumedAt" IS NULL
            """,
            new { userId, purpose = AuthTokenPurpose.PasswordReset })).ToList();

        open.ShouldBe([AuthTokenHasher.Hash(second)]);
        open.ShouldNotContain(AuthTokenHasher.Hash(first));
    }

    [Fact]
    public async Task MarkSignedIn_FillsLastSignInAt()
    {
        Assert.SkipWhen(string.IsNullOrWhiteSpace(ConnectionString), $"{EnvVar} is not set.");
        var factory = new TestFactory(ConnectionString!);
        var userId = await AnyUserIdAsync(factory);

        await new AuthRepository(factory).MarkSignedInAsync(userId, default);

        using var connection = await factory.CreateDbConnectionAsync();
        var at = await connection.ExecuteScalarAsync<DateTime?>("""SELECT "LastSignInAt" FROM "unshackled-word"."Users" WHERE "Id" = @userId""", new { userId });
        at.ShouldNotBeNull();
    }

    internal static async Task<int> CreateUserAsync(TestFactory factory, string[] roles, bool deactivated = false, string? name = null)
    {
        using var connection = await factory.CreateDbConnectionAsync();
        var tag = Guid.NewGuid().ToString("N");
        var id = await connection.ExecuteScalarAsync<int>(
            """
            INSERT INTO "unshackled-word"."Users" ("Email", "EmailNormalized", "DisplayName")
            VALUES (@email, lower(@email), @name)
            RETURNING "Id"
            """,
            new { email = $"{tag}@example.org", name = name ?? $"Test {tag}" });

        foreach (var role in roles)
        {
            await connection.ExecuteAsync("""INSERT INTO "unshackled-word"."UserRoles" ("UserId", "Role") VALUES (@id, @role)""", new { id, role });
        }

        if (deactivated)
        {
            await connection.ExecuteAsync(
                """UPDATE "unshackled-word"."Users" SET "DeactivatedAt" = now(), "DeactivatedByUserId" = @id WHERE "Id" = @id""", new { id });
        }

        return id;
    }

    internal static UserAdminRepository NewRepository(TestFactory factory) => new(factory, new AdminLogRepository(factory));

    private static async Task<string> StampAsync(TestFactory factory, int id)
    {
        using var connection = await factory.CreateDbConnectionAsync();
        return await connection.ExecuteScalarAsync<string>("""SELECT "SecurityStamp" FROM "unshackled-word"."Users" WHERE "Id" = @id""", new { id }) ?? "";
    }

    [Fact]
    public async Task SetRoles_GrantAndRevoke_LogsOneRowPerRole_RotatesStamp_AndSetsGrantedBy()
    {
        Assert.SkipWhen(string.IsNullOrWhiteSpace(ConnectionString), $"{EnvVar} is not set.");
        var factory = new TestFactory(ConnectionString!);
        var repository = NewRepository(factory);
        var actor = await CreateUserAsync(factory, ["user", "admin"]);
        var target = await CreateUserAsync(factory, ["user", "reviewer"]);
        var stampBefore = await StampAsync(factory, target);

        var outcome = await repository.SetRolesAsync(actor, target, ["maintainer", "maintainer"]);

        outcome.Error.ShouldBeNull();
        outcome.Detail!.Roles.ShouldBe(["maintainer", "user"]);
        (await StampAsync(factory, target)).ShouldNotBe(stampBefore);

        using var connection = await factory.CreateDbConnectionAsync();
        (await connection.ExecuteScalarAsync<int?>(
            """SELECT "GrantedByUserId" FROM "unshackled-word"."UserRoles" WHERE "UserId" = @target AND "Role" = 'maintainer'""", new { target }))
            .ShouldBe(actor);

        var log = await new AdminLogRepository(factory).QueryAsync(new AdminLogQuery(TargetUserId: target));
        log.Select(l => l.Action).Order().ToArray().ShouldBe([AdminLogActions.RoleGranted, AdminLogActions.RoleRevoked]);
        log.ShouldAllBe(l => l.ActorUserId == actor);
    }

    [Fact]
    public async Task SetRoles_NoChange_LogsNothing_AndKeepsTheStamp()
    {
        Assert.SkipWhen(string.IsNullOrWhiteSpace(ConnectionString), $"{EnvVar} is not set.");
        var factory = new TestFactory(ConnectionString!);
        var repository = NewRepository(factory);
        var actor = await CreateUserAsync(factory, ["user", "admin"]);
        var target = await CreateUserAsync(factory, ["user", "reviewer"]);
        var stampBefore = await StampAsync(factory, target);

        var outcome = await repository.SetRolesAsync(actor, target, ["reviewer"]);

        outcome.Detail!.Roles.ShouldBe(["reviewer", "user"]);
        (await new AdminLogRepository(factory).QueryAsync(new AdminLogQuery(TargetUserId: target))).ShouldBeEmpty();
        (await StampAsync(factory, target)).ShouldBe(stampBefore);
    }

    [Fact]
    public async Task SetRoles_OwnAdminRole_AiUser_AndUnknownUser_AreRefusedWithoutChange()
    {
        Assert.SkipWhen(string.IsNullOrWhiteSpace(ConnectionString), $"{EnvVar} is not set.");
        var factory = new TestFactory(ConnectionString!);
        var repository = NewRepository(factory);
        var admin = await CreateUserAsync(factory, ["user", "admin"]);
        var ai = await CreateUserAsync(factory, ["user", "ai"]);

        (await repository.SetRolesAsync(admin, admin, [])).Error.ShouldBe(UserAdminErrors.OwnAdminRole);
        (await repository.SetRolesAsync(admin, ai, ["reviewer"])).Error.ShouldBe(UserAdminErrors.AiUser);
        (await repository.SetRolesAsync(admin, int.MaxValue, ["reviewer"])).NotFound.ShouldBeTrue();

        (await repository.GetDetailAsync(admin, admin))!.Roles.ShouldBe(["admin", "user"]);
        (await repository.GetDetailAsync(ai, admin))!.Roles.ShouldBe(["ai", "user"]);
        (await new AdminLogRepository(factory).QueryAsync(new AdminLogQuery(TargetUserId: ai))).ShouldBeEmpty();
    }

    [Fact]
    public async Task SetRoles_LastActiveAdmin_CannotBeDemoted_ButADeactivatedAdminCan()
    {
        Assert.SkipWhen(string.IsNullOrWhiteSpace(ConnectionString), $"{EnvVar} is not set.");
        var factory = new TestFactory(ConnectionString!);
        var repository = NewRepository(factory);

        using var connection = await factory.CreateDbConnectionAsync();
        var others = await connection.ExecuteScalarAsync<int>(
            """
            SELECT count(*)
            FROM "unshackled-word"."UserRoles" r
            JOIN "unshackled-word"."Users" u ON u."Id" = r."UserId"
            WHERE r."Role" = 'admin' AND u."DeactivatedAt" IS NULL
            """);
        Assert.SkipWhen(others > 0, "The database copy already has active admins; the last-admin case needs a copy without any.");

        var onlyAdmin = await CreateUserAsync(factory, ["user", "admin"]);
        var actor = await CreateUserAsync(factory, ["user", "maintainer"]);

        (await repository.SetRolesAsync(actor, onlyAdmin, [])).Error.ShouldBe(UserAdminErrors.LastAdmin);

        var gone = await CreateUserAsync(factory, ["user", "admin"], deactivated: true);
        (await repository.SetRolesAsync(actor, gone, [])).Error.ShouldBeNull();
    }

    [Fact]
    public async Task List_FiltersBySearchRoleAndStatus_AndPages()
    {
        Assert.SkipWhen(string.IsNullOrWhiteSpace(ConnectionString), $"{EnvVar} is not set.");
        var factory = new TestFactory(ConnectionString!);
        var repository = NewRepository(factory);
        var tag = Guid.NewGuid().ToString("N");

        await CreateUserAsync(factory, ["user"], name: $"M\u00fcller_a{tag}");
        await CreateUserAsync(factory, ["user", "reviewer"], name: $"M\u00fcller_b{tag}");
        var gone = await CreateUserAsync(factory, ["user"], deactivated: true, name: $"M\u00fcller_c{tag}");
        var ai = await CreateUserAsync(factory, ["user", "ai"], name: $"M\u00fcller_d{tag}");

        (await repository.ListAsync(new UserListQuery("m\u00fcller_", null, null))).Total.ShouldBeGreaterThanOrEqualTo(4);
        // "_" matches literally, not as a wildcard.
        (await repository.ListAsync(new UserListQuery($"m\u00fcllerX_a{tag}", null, null))).Total.ShouldBe(0);
        (await repository.ListAsync(new UserListQuery($"m\u00fcller_a{tag}", null, null))).Total.ShouldBe(1);
        (await repository.ListAsync(new UserListQuery(tag, "reviewer", null))).Items.Single().Roles.ShouldBe(["reviewer", "user"]);
        (await repository.ListAsync(new UserListQuery(tag, null, "deactivated"))).Items.Single().Id.ShouldBe(gone);
        (await repository.ListAsync(new UserListQuery(tag, null, "ai"))).Items.Single().Id.ShouldBe(ai);

        var page2 = await repository.ListAsync(new UserListQuery(tag, null, "active", Page: 2, PageSize: 1));
        page2.Total.ShouldBe(2);
        page2.Items.Length.ShouldBe(1);
        (await repository.ListAsync(new UserListQuery(tag, null, null, Page: 9, PageSize: 10))).Total.ShouldBe(4);
    }

    private static async Task<(string? PasswordHash, bool Verified)> AccountAsync(TestFactory factory, int id)
    {
        using var connection = await factory.CreateDbConnectionAsync();
        var row = await connection.QuerySingleAsync<(string? Hash, bool Verified)>(
            """SELECT "PasswordHash", "EmailVerifiedAt" IS NOT NULL FROM "unshackled-word"."Users" WHERE "Id" = @id""", new { id });
        return (row.Hash, row.Verified);
    }

    [Fact]
    public async Task Create_MakesVerifiedAccountWithoutPassword_WithRoles_AndRefusesDuplicateEmail()
    {
        Assert.SkipWhen(string.IsNullOrWhiteSpace(ConnectionString), $"{EnvVar} is not set.");
        var factory = new TestFactory(ConnectionString!);
        var repository = NewRepository(factory);
        var actor = await CreateUserAsync(factory, ["user", "admin"]);
        var email = $"Anna.{Guid.NewGuid():N}@Example.org";

        var created = await repository.CreateAsync(actor, $" {email} ", "  Anna  ", ["reviewer", "reviewer", "ai", "bogus"]);

        created.ExistingUserId.ShouldBeNull();
        var detail = created.Detail!;
        detail.Email.ShouldBe(email);
        detail.DisplayName.ShouldBe("Anna");
        detail.Roles.ShouldBe(["reviewer", "user"]);
        detail.EmailVerified.ShouldBeTrue();
        detail.HasPassword.ShouldBeFalse();
        (await AccountAsync(factory, detail.Id)).ShouldBe((null, true));

        using var connection = await factory.CreateDbConnectionAsync();
        (await connection.ExecuteScalarAsync<int?>(
            """SELECT "GrantedByUserId" FROM "unshackled-word"."UserRoles" WHERE "UserId" = @id AND "Role" = 'reviewer'""", new { id = detail.Id }))
            .ShouldBe(actor);

        var log = (await new AdminLogRepository(factory).QueryAsync(new AdminLogQuery(TargetUserId: detail.Id))).Single();
        log.Action.ShouldBe(AdminLogActions.UserCreated);
        log.ActorUserId.ShouldBe(actor);
        log.DetailsJson.ShouldContain(email);
        log.DetailsJson.ShouldContain("reviewer");

        // Same address in other case and with whitespace: 409 with the existing id, nothing written.
        var again = await repository.CreateAsync(actor, $"  {email.ToLowerInvariant()}", "Anna 2", []);
        again.ExistingUserId.ShouldBe(detail.Id);
        again.Detail.ShouldBeNull();
        (await new AdminLogRepository(factory).QueryAsync(new AdminLogQuery(TargetUserId: detail.Id))).Count.ShouldBe(1);
    }

    [Fact]
    public async Task Update_EmailChange_VerifiesRotatesStamp_EndsOpenLinks_AndLogsOldAndNew()
    {
        Assert.SkipWhen(string.IsNullOrWhiteSpace(ConnectionString), $"{EnvVar} is not set.");
        var factory = new TestFactory(ConnectionString!);
        var repository = NewRepository(factory);
        var actor = await CreateUserAsync(factory, ["user", "admin"]);
        var target = await CreateUserAsync(factory, ["user"], name: "Old Name");
        var stampBefore = await StampAsync(factory, target);
        var open = await new AuthRepository(factory).IssueTokenAsync(target, AuthTokenPurpose.PasswordReset, TimeSpan.FromHours(1), true, default);
        var newEmail = $"new.{Guid.NewGuid():N}@example.org";

        var before = (await repository.GetDetailAsync(target, actor))!;
        var outcome = await repository.UpdateAsync(actor, target, " New Name ", newEmail);

        outcome.Error.ShouldBeNull();
        outcome.PreviousEmail.ShouldBe(before.Email);
        outcome.Detail!.Email.ShouldBe(newEmail);
        outcome.Detail.DisplayName.ShouldBe("New Name");
        outcome.Detail.EmailVerified.ShouldBeTrue();
        (await StampAsync(factory, target)).ShouldNotBe(stampBefore);
        (await new AuthRepository(factory).FindByEmailAsync(EmailNormalizer.Normalize(newEmail), default))!.Id.ShouldBe(target);
        (await new AuthRepository(factory).FindByEmailAsync(EmailNormalizer.Normalize(before.Email), default)).ShouldBeNull();
        (await new AuthRepository(factory).ConsumePasswordResetAsync(AuthTokenHasher.Hash(open), "hash", default)).ShouldBeNull();

        var log = (await new AdminLogRepository(factory).QueryAsync(new AdminLogQuery(TargetUserId: target))).Single();
        log.Action.ShouldBe(AdminLogActions.UserUpdated);
        using var json = System.Text.Json.JsonDocument.Parse(log.DetailsJson);
        json.RootElement.GetProperty("email").GetProperty("old").GetString().ShouldBe(before.Email);
        json.RootElement.GetProperty("email").GetProperty("new").GetString().ShouldBe(newEmail);
        json.RootElement.GetProperty("displayName").GetProperty("old").GetString().ShouldBe("Old Name");
    }

    [Fact]
    public async Task Update_NameOnly_KeepsStamp_LogsOnlyName_AndNoChangeLogsNothing()
    {
        Assert.SkipWhen(string.IsNullOrWhiteSpace(ConnectionString), $"{EnvVar} is not set.");
        var factory = new TestFactory(ConnectionString!);
        var repository = NewRepository(factory);
        var actor = await CreateUserAsync(factory, ["user", "admin"]);
        var target = await CreateUserAsync(factory, ["user"], name: "Anna");
        var email = (await repository.GetDetailAsync(target, actor))!.Email;
        var stampBefore = await StampAsync(factory, target);

        var same = await repository.UpdateAsync(actor, target, "Anna", email.ToUpperInvariant());
        same.Detail!.Email.ShouldBe(email);
        same.PreviousEmail.ShouldBeNull();
        (await new AdminLogRepository(factory).QueryAsync(new AdminLogQuery(TargetUserId: target))).ShouldBeEmpty();

        var renamed = await repository.UpdateAsync(actor, target, "Anne", email);
        renamed.PreviousEmail.ShouldBeNull();
        (await StampAsync(factory, target)).ShouldBe(stampBefore);
        var log = (await new AdminLogRepository(factory).QueryAsync(new AdminLogQuery(TargetUserId: target))).Single();
        log.DetailsJson.ShouldContain("displayName");
        log.DetailsJson.ShouldNotContain("\"email\"");
    }

    [Fact]
    public async Task Update_DuplicateEmail_AiUser_AndUnknownUser_AreRefused()
    {
        Assert.SkipWhen(string.IsNullOrWhiteSpace(ConnectionString), $"{EnvVar} is not set.");
        var factory = new TestFactory(ConnectionString!);
        var repository = NewRepository(factory);
        var actor = await CreateUserAsync(factory, ["user", "admin"]);
        var target = await CreateUserAsync(factory, ["user"], name: "Target");
        var other = await CreateUserAsync(factory, ["user"]);
        var ai = await CreateUserAsync(factory, ["user", "ai"]);
        var otherEmail = (await repository.GetDetailAsync(other, actor))!.Email;
        var stampBefore = await StampAsync(factory, target);

        var taken = await repository.UpdateAsync(actor, target, "Renamed", otherEmail.ToUpperInvariant());
        taken.Error.ShouldBe(UserAdminErrors.EmailTaken);
        taken.ExistingUserId.ShouldBe(other);
        (await repository.GetDetailAsync(target, actor))!.DisplayName.ShouldBe("Target");
        (await StampAsync(factory, target)).ShouldBe(stampBefore);

        (await repository.UpdateAsync(actor, ai, "X", "x@example.org")).Error.ShouldBe(UserAdminErrors.AiUser);
        (await repository.UpdateAsync(actor, int.MaxValue, "X", "x@example.org")).NotFound.ShouldBeTrue();
        (await new AdminLogRepository(factory).QueryAsync(new AdminLogQuery(TargetUserId: target))).ShouldBeEmpty();
    }

    [Fact]
    public async Task FindResetTarget_ReportsPasswordAiAndDeactivatedState()
    {
        Assert.SkipWhen(string.IsNullOrWhiteSpace(ConnectionString), $"{EnvVar} is not set.");
        var factory = new TestFactory(ConnectionString!);
        var repository = NewRepository(factory);
        var plain = await CreateUserAsync(factory, ["user"]);
        var ai = await CreateUserAsync(factory, ["user", "ai"]);
        var gone = await CreateUserAsync(factory, ["user"], deactivated: true);

        using (var connection = await factory.CreateDbConnectionAsync())
        {
            await connection.ExecuteAsync("""UPDATE "unshackled-word"."Users" SET "PasswordHash" = 'x' WHERE "Id" = @plain""", new { plain });
        }

        (await repository.FindResetTargetAsync(plain))!.HasPassword.ShouldBeTrue();
        (await repository.FindResetTargetAsync(ai))!.IsAi.ShouldBeTrue();
        (await new AuthRepository(factory).IsDeactivatedAsync(gone, default)).ShouldBeTrue();
        (await new AuthRepository(factory).IsDeactivatedAsync(plain, default)).ShouldBeFalse();
        (await repository.FindResetTargetAsync(gone))!.HasPassword.ShouldBeFalse();
        (await repository.FindResetTargetAsync(int.MaxValue)).ShouldBeNull();
    }
}
