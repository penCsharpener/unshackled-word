using System.Text;
using Dapper;
using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Application.Features.UserAdmin;
using UnshackledWord.Domain.WebApi.Auth;
using UnshackledWord.Infrastructure.Repositories;
using UnshackledWord.Tooling.WebApi.Auth;
using Db = UnshackledWord.Tooling.WebApi.Tests.UserAdminDatabaseTests;
using Login = UnshackledWord.Tooling.WebApi.Endpoints.Auth.Login;
using MagicLinkLogin = UnshackledWord.Tooling.WebApi.Endpoints.Auth.MagicLinkLogin;
using PasskeyLogin = UnshackledWord.Tooling.WebApi.Endpoints.Auth.PasskeyLogin;
using RequestMagicLink = UnshackledWord.Tooling.WebApi.Endpoints.Auth.RequestMagicLink;
using RequestPasswordReset = UnshackledWord.Tooling.WebApi.Endpoints.Auth.RequestPasswordReset;
using ResetPassword = UnshackledWord.Tooling.WebApi.Endpoints.Auth.ResetPassword;
using VerifyEmail = UnshackledWord.Tooling.WebApi.Endpoints.Auth.VerifyEmail;

namespace UnshackledWord.Tooling.WebApi.Tests;

/// <summary>
/// Roadmap 0020.6 against a real Postgres (see <see cref="UserAdminDatabaseTests"/> for the setup): deactivate/reactivate and one test
/// for each of the six sign-in paths. Endpoints run in-process with a real <see cref="AuthRepository"/>; only mail and WebAuthn are faked.
/// </summary>
public class UserDeactivationTests
{
    private const string Password = "correct horse battery staple";

    private static Db.TestFactory Factory() => new(Db.ConnectionString!);

    private static void RequireDatabase() =>
        Assert.SkipWhen(string.IsNullOrWhiteSpace(Db.ConnectionString), $"{Db.EnvVar} is not set.");

    private static async Task<int> NewMemberAsync(Db.TestFactory factory, bool withPassword = true)
    {
        var id = await Db.CreateUserAsync(factory, ["user"]);
        using var connection = await factory.CreateDbConnectionAsync();
        await connection.ExecuteAsync(
            """
            UPDATE "unshackled-word"."Users"
            SET "EmailVerifiedAt" = now(), "PasswordHash" = @hash
            WHERE "Id" = @id
            """,
            new { id, hash = withPassword ? new BCryptPasswordHasher().Hash(Password) : null });
        return id;
    }

    private static async Task<string> EmailOfAsync(Db.TestFactory factory, int id)
    {
        using var connection = await factory.CreateDbConnectionAsync();
        return await connection.ExecuteScalarAsync<string>("""SELECT "Email" FROM "unshackled-word"."Users" WHERE "Id" = @id""", new { id }) ?? "";
    }

    private static async Task<int> AdminAsync(Db.TestFactory factory) => await Db.CreateUserAsync(factory, ["user", "admin"]);

    private static async Task DeactivateAsync(Db.TestFactory factory, int actor, int target) =>
        (await Db.NewRepository(factory).DeactivateAsync(actor, target, "Projekt verlassen")).Error.ShouldBeNull();

    private static (T Endpoint, DefaultHttpContext Context) Create<T>(params object[] dependencies)
        where T : class, FastEndpoints.IEndpoint
    {
        DefaultHttpContext? captured = null;
        var endpoint = FastEndpoints.Factory.Create<T>(
            ctx =>
            {
                ctx.RequestServices = new ServiceCollection().AddLogging().AddOptions().BuildServiceProvider();
                ctx.Response.Body = new MemoryStream();
                captured = ctx;
            },
            dependencies);
        return (endpoint, captured!);
    }

    private static string Body(HttpContext context)
    {
        context.Response.Body.Position = 0;
        return new StreamReader(context.Response.Body, Encoding.UTF8).ReadToEnd();
    }

    private sealed class RecordingSender : IEmailSender
    {
        public List<EmailMessage> Sent { get; } = [];

        public Task SendAsync(EmailMessage message, CancellationToken ct)
        {
            Sent.Add(message);
            return Task.CompletedTask;
        }
    }

    // ---- deactivate / reactivate ----

    [Fact]
    public async Task Deactivate_MarksAccount_RotatesStamp_ConsumesOpenTokens_AndLogs()
    {
        RequireDatabase();
        var factory = Factory();
        var repository = Db.NewRepository(factory);
        var actor = await AdminAsync(factory);
        var target = await NewMemberAsync(factory);
        var auth = new AuthRepository(factory);
        var stampBefore = (await auth.GetSecurityStampAsync(target, default))!;
        await auth.IssueTokenAsync(target, AuthTokenPurpose.PasswordReset, TimeSpan.FromHours(1), supersedeOpen: false, default);
        await auth.IssueTokenAsync(target, AuthTokenPurpose.MagicLink, TimeSpan.FromHours(1), supersedeOpen: false, default);

        var outcome = await repository.DeactivateAsync(actor, target, "  Projekt verlassen  ");

        outcome.Error.ShouldBeNull();
        outcome.Detail!.Status.ShouldBe("deactivated");
        outcome.Detail.Deactivation!.Reason.ShouldBe("Projekt verlassen");
        outcome.Detail.Deactivation.ByUserId.ShouldBe(actor);

        using var connection = await factory.CreateDbConnectionAsync();
        (await connection.ExecuteScalarAsync<int>(
            """SELECT count(*) FROM "unshackled-word"."AuthTokens" WHERE "UserId" = @target AND "ConsumedAt" IS NULL""", new { target })).ShouldBe(0);
        (await connection.ExecuteScalarAsync<string>(
            """SELECT "SecurityStamp" FROM "unshackled-word"."Users" WHERE "Id" = @target""", new { target })).ShouldNotBe(stampBefore);

        var log = (await new AdminLogRepository(factory).QueryAsync(new AdminLogQuery(TargetUserId: target))).Single();
        log.Action.ShouldBe(AdminLogActions.UserDeactivated);
        log.ActorUserId.ShouldBe(actor);
        log.DetailsJson.ShouldContain("Projekt verlassen");
    }

    [Fact]
    public async Task Deactivate_WithoutReason_OmitsReasonFromLog()
    {
        RequireDatabase();
        var factory = Factory();
        var actor = await AdminAsync(factory);
        var target = await NewMemberAsync(factory);

        (await Db.NewRepository(factory).DeactivateAsync(actor, target, "   ")).Detail!.Deactivation!.Reason.ShouldBeNull();

        var log = (await new AdminLogRepository(factory).QueryAsync(new AdminLogQuery(TargetUserId: target))).Single();
        log.DetailsJson.ShouldNotContain("reason");
    }

    [Fact]
    public async Task Deactivate_Guards_SelfAiAlreadyDeactivatedUnknown_ChangeNothing()
    {
        RequireDatabase();
        var factory = Factory();
        var repository = Db.NewRepository(factory);
        var actor = await AdminAsync(factory);
        var ai = await Db.CreateUserAsync(factory, ["user", "ai"]);
        var gone = await Db.CreateUserAsync(factory, ["user"], deactivated: true);

        (await repository.DeactivateAsync(actor, actor, null)).Error.ShouldBe(UserAdminErrors.Self);
        (await repository.DeactivateAsync(actor, ai, null)).Error.ShouldBe(UserAdminErrors.AiUser);
        (await repository.DeactivateAsync(actor, gone, null)).Error.ShouldBe(UserAdminErrors.AlreadyDeactivated);
        (await repository.DeactivateAsync(actor, int.MaxValue, null)).NotFound.ShouldBeTrue();

        (await new AdminLogRepository(factory).QueryAsync(new AdminLogQuery(TargetUserId: ai))).ShouldBeEmpty();
        (await new AuthRepository(factory).IsDeactivatedAsync(actor, default)).ShouldBeFalse();
    }

    [Fact]
    public async Task Deactivate_LastActiveAdmin_IsRefused_ButANonLastAdminCanBe()
    {
        RequireDatabase();
        var factory = Factory();
        var repository = Db.NewRepository(factory);

        using var connection = await factory.CreateDbConnectionAsync();
        var others = await connection.ExecuteScalarAsync<int>(
            """
            SELECT count(*)
            FROM "unshackled-word"."UserRoles" r
            JOIN "unshackled-word"."Users" u ON u."Id" = r."UserId"
            WHERE r."Role" = 'admin' AND u."DeactivatedAt" IS NULL
            """);
        Assert.SkipWhen(others > 0, "The database copy already has active admins; the last-admin case needs a copy without any.");

        var onlyAdmin = await AdminAsync(factory);
        var maintainer = await Db.CreateUserAsync(factory, ["user", "maintainer"]);

        (await repository.DeactivateAsync(maintainer, onlyAdmin, null)).Error.ShouldBe(UserAdminErrors.LastAdmin);

        var second = await AdminAsync(factory);
        (await repository.DeactivateAsync(maintainer, second, null)).Error.ShouldBeNull();
    }

    [Fact]
    public async Task Reactivate_ClearsDeactivation_KeepsStamp_LogsPreviousValues_AndRefusesActiveAccounts()
    {
        RequireDatabase();
        var factory = Factory();
        var repository = Db.NewRepository(factory);
        var actor = await AdminAsync(factory);
        var target = await NewMemberAsync(factory);
        var auth = new AuthRepository(factory);

        (await repository.ReactivateAsync(actor, target)).Error.ShouldBe(UserAdminErrors.NotDeactivated);

        await Db.NewRepository(factory).DeactivateAsync(actor, target, "Pause");
        var stampWhileDeactivated = await StampAsync(factory, target);

        var outcome = await repository.ReactivateAsync(actor, target);

        outcome.Error.ShouldBeNull();
        outcome.Detail!.Status.ShouldBe("active");
        outcome.Detail.Deactivation.ShouldBeNull();
        (await StampAsync(factory, target)).ShouldBe(stampWhileDeactivated);
        (await auth.GetSecurityStampAsync(target, default)).ShouldNotBeNull();

        var logs = await new AdminLogRepository(factory).QueryAsync(new AdminLogQuery(TargetUserId: target));
        logs.Select(l => l.Action).ShouldBe([AdminLogActions.UserReactivated, AdminLogActions.UserDeactivated]);
        logs[0].DetailsJson.ShouldContain("Pause");
        logs[0].DetailsJson.ShouldContain("deactivatedAt");

        (await repository.ReactivateAsync(actor, int.MaxValue)).NotFound.ShouldBeTrue();
    }

    private static async Task<string> StampAsync(Db.TestFactory factory, int id)
    {
        using var connection = await factory.CreateDbConnectionAsync();
        return await connection.ExecuteScalarAsync<string>("""SELECT "SecurityStamp" FROM "unshackled-word"."Users" WHERE "Id" = @id""", new { id }) ?? "";
    }

    // ---- sign-in path 1: existing cookie ----

    [Fact]
    public async Task Cookie_StampLookup_ReturnsNullForDeactivatedAccount_AndTheStampAgainAfterReactivation()
    {
        RequireDatabase();
        var factory = Factory();
        var auth = new AuthRepository(factory);
        var actor = await AdminAsync(factory);
        var target = await NewMemberAsync(factory);
        (await auth.GetSecurityStampAsync(target, default)).ShouldNotBeNull();

        await DeactivateAsync(factory, actor, target);
        (await auth.GetSecurityStampAsync(target, default)).ShouldBeNull();

        // Even a cookie carrying the old stamp has nothing to match once the lookup is null.
        await Db.NewRepository(factory).ReactivateAsync(actor, target);
        (await auth.GetSecurityStampAsync(target, default)).ShouldNotBeNull();
    }

    // ---- sign-in path 2: password login ----

    [Fact]
    public async Task Login_DeactivatedAccount_Gets403AccountDeactivated_OnlyAfterTheCorrectPassword()
    {
        RequireDatabase();
        var factory = Factory();
        var actor = await AdminAsync(factory);
        var target = await NewMemberAsync(factory);
        await DeactivateAsync(factory, actor, target);
        var email = await EmailOfAsync(factory, target);

        var (wrong, wrongContext) = Create<Login.Endpoint>(new AuthRepository(factory), new BCryptPasswordHasher(), new SessionSignIn());
        await wrong.HandleAsync(new LoginRequest { Email = email, Password = "wrong password" }, default);
        wrongContext.Response.StatusCode.ShouldBe(StatusCodes.Status401Unauthorized);

        var (right, rightContext) = Create<Login.Endpoint>(new AuthRepository(factory), new BCryptPasswordHasher(), new SessionSignIn());
        await right.HandleAsync(new LoginRequest { Email = email, Password = Password }, default);
        rightContext.Response.StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
        Body(rightContext).ShouldContain("\"code\":\"account_deactivated\"");
    }

    // ---- sign-in path 3: magic link (request and verify) ----

    [Fact]
    public async Task MagicLink_DeactivatedAccount_GetsNoMailOnRequest_AndALinkIssuedAnywayIsRefused()
    {
        RequireDatabase();
        var factory = Factory();
        var auth = new AuthRepository(factory);
        var actor = await AdminAsync(factory);
        var target = await NewMemberAsync(factory);
        await DeactivateAsync(factory, actor, target);
        var email = await EmailOfAsync(factory, target);

        var sender = new RecordingSender();
        var (request, requestContext) = Create<RequestMagicLink.Endpoint>(auth, new AuthEmailBuilder(Microsoft.Extensions.Options.Options.Create(new AppOptions())), sender);
        await request.HandleAsync(new EmailRequest { Email = email }, default);
        requestContext.Response.StatusCode.ShouldBe(StatusCodes.Status202Accepted);
        sender.Sent.ShouldBeEmpty();

        var token = await auth.IssueTokenAsync(target, AuthTokenPurpose.MagicLink, TimeSpan.FromMinutes(15), supersedeOpen: false, default);
        var (verify, verifyContext) = Create<MagicLinkLogin.Endpoint>(auth, new SessionSignIn());
        await verify.HandleAsync(new TokenRequest { Token = token }, default);
        verifyContext.Response.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
    }

    // ---- sign-in path 4: passkey ----

    [Fact]
    public async Task Passkey_DeactivatedAccount_Gets403_AfterTheSignatureCheck()
    {
        RequireDatabase();
        var factory = Factory();
        var auth = new AuthRepository(factory);
        var actor = await AdminAsync(factory);
        var target = await NewMemberAsync(factory);
        var credentialId = Guid.NewGuid().ToByteArray();
        await auth.InsertPasskeyAsync(target, credentialId, [1, 2, 3], 0, [], Guid.Empty, "Test", default);
        await DeactivateAsync(factory, actor, target);

        var fido2 = Substitute.For<IFido2>();
        fido2.MakeAssertionAsync(Arg.Any<MakeAssertionParams>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new VerifyAssertionResult { SignCount = 1 }));
        var ceremonies = new PasskeyCeremonyStore(new MemoryCache(new MemoryCacheOptions()));
        ceremonies.SetLogin("flow", new AssertionOptions());

        var (endpoint, context) = Create<PasskeyLogin.Endpoint>(auth, fido2, ceremonies, new SessionSignIn(), NullLogger<PasskeyLogin.Endpoint>.Instance);
        await endpoint.HandleAsync(
            new PasskeyLogin.PasskeyLoginRequest
            {
                FlowId = "flow",
                Credential = new AuthenticatorAssertionRawResponse { RawId = credentialId, Response = new AuthenticatorAssertionRawResponse.AssertionResponse { AuthenticatorData = [1], ClientDataJson = [1], Signature = [1] } },
            },
            default);

        await fido2.Received(1).MakeAssertionAsync(Arg.Any<MakeAssertionParams>(), Arg.Any<CancellationToken>());
        context.Response.StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
        Body(context).ShouldContain("\"code\":\"account_deactivated\"");
    }

    // ---- sign-in path 5: password reset (request and consume) ----

    [Fact]
    public async Task PasswordReset_DeactivatedAccount_GetsNoMailOnRequest_AndAnOutstandingLinkIsRefused()
    {
        RequireDatabase();
        var factory = Factory();
        var auth = new AuthRepository(factory);
        var actor = await AdminAsync(factory);
        var target = await NewMemberAsync(factory);
        var openToken = await auth.IssueTokenAsync(target, AuthTokenPurpose.PasswordReset, TimeSpan.FromHours(1), supersedeOpen: false, default);
        await DeactivateAsync(factory, actor, target);
        var email = await EmailOfAsync(factory, target);

        var sender = new RecordingSender();
        var (request, requestContext) = Create<RequestPasswordReset.Endpoint>(auth, new AuthEmailBuilder(Microsoft.Extensions.Options.Options.Create(new AppOptions())), sender);
        await request.HandleAsync(new EmailRequest { Email = email }, default);
        requestContext.Response.StatusCode.ShouldBe(StatusCodes.Status202Accepted);
        sender.Sent.ShouldBeEmpty();

        // The link mailed before deactivation was consumed by it; a freshly issued one is refused by the join on Users.
        var fresh = await auth.IssueTokenAsync(target, AuthTokenPurpose.PasswordReset, TimeSpan.FromHours(1), supersedeOpen: false, default);
        foreach (var token in new[] { openToken, fresh })
        {
            var (reset, resetContext) = Create<ResetPassword.Endpoint>(auth, new BCryptPasswordHasher());
            await reset.HandleAsync(new ResetPasswordRequest { Token = token, NewPassword = "a brand new password 1" }, default);
            resetContext.Response.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
        }

        // Password untouched: the old one still verifies.
        (await auth.FindByEmailAsync(EmailNormalizer.Normalize(email), default))!.PasswordHash
            .ShouldSatisfyAllConditions(h => new BCryptPasswordHasher().Verify(Password, h!).ShouldBeTrue());
    }

    // ---- sign-in path 6: email verification ----

    [Fact]
    public async Task VerifyEmail_DeactivatedAccount_IsRefused_AndWorksAgainAfterReactivation()
    {
        RequireDatabase();
        var factory = Factory();
        var auth = new AuthRepository(factory);
        var actor = await AdminAsync(factory);
        var target = await NewMemberAsync(factory);
        await DeactivateAsync(factory, actor, target);
        var token = await auth.IssueTokenAsync(target, AuthTokenPurpose.EmailVerification, TimeSpan.FromHours(1), supersedeOpen: false, default);

        var (refused, refusedContext) = Create<VerifyEmail.Endpoint>(auth);
        await refused.HandleAsync(new TokenRequest { Token = token }, default);
        refusedContext.Response.StatusCode.ShouldBe(StatusCodes.Status404NotFound);

        await Db.NewRepository(factory).ReactivateAsync(actor, target);

        var (allowed, allowedContext) = Create<VerifyEmail.Endpoint>(auth);
        await allowed.HandleAsync(new TokenRequest { Token = token }, default);
        allowedContext.Response.StatusCode.ShouldBe(StatusCodes.Status204NoContent);
    }

    // ---- shared lookup ----

    [Fact]
    public async Task IsDeactivatedLookup_AndUserFlags_AgreeForEveryPath()
    {
        RequireDatabase();
        var factory = Factory();
        var auth = new AuthRepository(factory);
        var actor = await AdminAsync(factory);
        var target = await NewMemberAsync(factory);
        var email = EmailNormalizer.Normalize(await EmailOfAsync(factory, target));
        var credentialId = Guid.NewGuid().ToByteArray();
        await auth.InsertPasskeyAsync(target, credentialId, [1], 0, [], Guid.Empty, "Test", default);

        (await auth.IsDeactivatedAsync(target, default)).ShouldBeFalse();
        (await auth.FindByEmailAsync(email, default))!.IsDeactivated.ShouldBeFalse();

        await DeactivateAsync(factory, actor, target);

        (await auth.IsDeactivatedAsync(target, default)).ShouldBeTrue();
        (await auth.FindByEmailAsync(email, default))!.IsDeactivated.ShouldBeTrue();
        (await auth.FindByIdAsync(target, default))!.IsDeactivated.ShouldBeTrue();
        (await auth.FindByCredentialIdAsync(credentialId, default))!.IsDeactivated.ShouldBeTrue();
        (await auth.IsDeactivatedAsync(int.MaxValue, default)).ShouldBeFalse();
    }
}
