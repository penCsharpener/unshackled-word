using UnshackledWord.Application.Features.ApiTokens;
using UnshackledWord.Application.Features.Auth;

namespace UnshackledWord.Application.Tests.Features.ApiTokens;

public class ApiTokenServiceTests
{
    private readonly IApiTokenRepository _repository = Substitute.For<IApiTokenRepository>();
    private readonly ManualTime _time = new(new DateTimeOffset(2026, 11, 16, 12, 0, 0, TimeSpan.Zero));
    private ApiTokenRecord? _stored;
    private string? _storedHash;
    private IReadOnlyList<string>? _roles = [AppRoles.Ai];

    public ApiTokenServiceTests()
    {
        _repository.GetUserRolesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(_ => _roles);
        _repository.InsertAsync(Arg.Any<ApiTokenInsert>(), Arg.Any<CancellationToken>()).Returns(c =>
        {
            var insert = c.Arg<ApiTokenInsert>();
            _storedHash = insert.TokenHash;
            _stored = new ApiTokenRecord { Id = 9, UserId = insert.UserId, Name = insert.Name, TokenPrefix = insert.TokenPrefix, Scope = insert.Scope, CreatedByUserId = insert.CreatedByUserId, CreatedAt = _time.GetUtcNow().UtcDateTime, ExpiresAt = insert.ExpiresAt };

            return _stored;
        });
        _repository.FindByHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(c => c.Arg<string>() == _storedHash ? _stored : null);
    }

    private ApiTokenService Create() => new(_repository, _time);

    private async Task<string> CreateTokenAsync(int days = 30)
    {
        var result = await Create().CreateAsync(3, "claude-code", days, 1);
        result.Outcome.ShouldBe(ApiTokenCreateOutcome.Created);

        return result.Created!.Secret;
    }

    [Fact]
    public void Generate_ProducesPrefixedHighEntropyTokens_AndHashIsNotTheToken()
    {
        var a = ApiTokenService.Generate();
        var b = ApiTokenService.Generate();

        a.ShouldStartWith("uwt_");
        a.Length.ShouldBe(47);
        a.ShouldNotBe(b);
        a.ShouldMatch("^uwt_[A-Za-z0-9_-]{43}$");
        ApiTokenService.Hash(a).Length.ShouldBe(64);
        ApiTokenService.Hash(a).ShouldNotContain(a[4..]);
        ApiTokenService.Hash(a).ShouldBe(ApiTokenService.Hash(a));
    }

    [Fact]
    public async Task Create_StoresOnlyTheHash_AndExpiry()
    {
        var secret = await CreateTokenAsync(10);

        _storedHash.ShouldBe(ApiTokenService.Hash(secret));
        _stored!.TokenPrefix.ShouldBe(secret[..8]);
        _stored.ExpiresAt.ShouldBe(_time.GetUtcNow().UtcDateTime.AddDays(10));
        await _repository.DidNotReceive().InsertAsync(Arg.Is<ApiTokenInsert>(i => i.TokenHash == secret), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(0, 30)]
    [InlineData(-5, 30)]
    [InlineData(5000, 365)]
    public async Task Create_ClampsTheExpiry(int requested, int expectedDays)
    {
        await CreateTokenAsync(requested);

        _stored!.ExpiresAt.ShouldBe(_time.GetUtcNow().UtcDateTime.AddDays(expectedDays));
    }

    [Fact]
    public async Task Create_UnknownUser_And_UserWithoutRole_AreRefused()
    {
        _roles = null;
        (await Create().CreateAsync(3, "x", 30, 1)).Outcome.ShouldBe(ApiTokenCreateOutcome.UserNotFound);

        _roles = [AppRoles.User, AppRoles.Reviewer];
        (await Create().CreateAsync(3, "x", 30, 1)).Outcome.ShouldBe(ApiTokenCreateOutcome.UserNotAllowed);

        _roles = [AppRoles.Maintainer];
        (await Create().CreateAsync(3, "x", 30, 1)).Outcome.ShouldBe(ApiTokenCreateOutcome.Created);
    }

    [Fact]
    public async Task Validate_GoodToken_IsValid_AndTouchesLastUse()
    {
        var secret = await CreateTokenAsync();

        var result = await Create().ValidateAsync($"Bearer {secret}", ApiTokenScopes.Propose);

        result.IsValid.ShouldBeTrue();
        result.UserId.ShouldBe(3);
        result.TokenId.ShouldBe(9);
        await _repository.Received(1).TouchAsync(9, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Validate_AcceptsBearerCaseInsensitive_AndRawToken()
    {
        var secret = await CreateTokenAsync();

        (await Create().ValidateAsync($"bearer {secret}", ApiTokenScopes.Propose)).IsValid.ShouldBeTrue();
        (await Create().ValidateAsync(secret, ApiTokenScopes.Propose)).IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Bearer ")]
    public async Task Validate_NoToken_IsMissing(string? header)
    {
        (await Create().ValidateAsync(header, ApiTokenScopes.Propose)).Check.ShouldBe(ApiTokenCheck.Missing);
        await _repository.DidNotReceiveWithAnyArgs().FindByHashAsync(default!, default);
    }

    [Theory]
    [InlineData("Bearer abc")]
    [InlineData("Bearer xyz_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public async Task Validate_WrongShape_IsMalformed_WithoutDatabaseLookup(string header)
    {
        (await Create().ValidateAsync(header, ApiTokenScopes.Propose)).Check.ShouldBe(ApiTokenCheck.Malformed);
        await _repository.DidNotReceiveWithAnyArgs().FindByHashAsync(default!, default);
    }

    [Fact]
    public async Task Validate_UnknownToken_IsUnknown()
    {
        await CreateTokenAsync();

        var other = ApiTokenService.Generate();

        (await Create().ValidateAsync(other, ApiTokenScopes.Propose)).Check.ShouldBe(ApiTokenCheck.Unknown);
    }

    [Fact]
    public async Task Validate_RevokedToken_IsRevoked_AndNotTouched()
    {
        var secret = await CreateTokenAsync();
        _stored!.RevokedAt = _time.GetUtcNow().UtcDateTime;

        (await Create().ValidateAsync(secret, ApiTokenScopes.Propose)).Check.ShouldBe(ApiTokenCheck.Revoked);
        await _repository.DidNotReceive().TouchAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Validate_ExpiredToken_IsExpired()
    {
        var secret = await CreateTokenAsync(1);
        _time.Advance(TimeSpan.FromDays(1) + TimeSpan.FromSeconds(1));

        (await Create().ValidateAsync(secret, ApiTokenScopes.Propose)).Check.ShouldBe(ApiTokenCheck.Expired);
    }

    [Fact]
    public async Task Validate_WrongScope_IsRefused()
    {
        var secret = await CreateTokenAsync();

        (await Create().ValidateAsync(secret, "admin")).Check.ShouldBe(ApiTokenCheck.WrongScope);
    }

    [Fact]
    public async Task Validate_UserLostTheRole_IsRefused()
    {
        var secret = await CreateTokenAsync();
        _roles = [AppRoles.User];

        (await Create().ValidateAsync(secret, ApiTokenScopes.Propose)).Check.ShouldBe(ApiTokenCheck.UserNotAllowed);
    }

    [Fact]
    public async Task ListEligibleUsers_PutsAiUsersFirst_ThenSortsByName()
    {
        _repository.ListEligibleUsersAsync(Arg.Any<CancellationToken>()).Returns(
        [
            new ApiTokenUserRecord { Id = 1, Name = "zora", Roles = [AppRoles.Maintainer] },
            new ApiTokenUserRecord { Id = 2, Name = "ai:gemma4", Roles = [AppRoles.Ai] },
            new ApiTokenUserRecord { Id = 3, Name = "Anna", Roles = [AppRoles.Admin, AppRoles.Maintainer] },
            new ApiTokenUserRecord { Id = 4, Name = "ai:claude", Roles = [AppRoles.Ai] },
        ]);

        var users = await Create().ListEligibleUsersAsync();

        users.Select(u => u.Name).ShouldBe(["ai:claude", "ai:gemma4", "Anna", "zora"]);
    }

    [Fact]
    public void EveryFailure_HasAMessage_ThatNeverContainsTheToken()
    {
        foreach (var check in Enum.GetValues<ApiTokenCheck>())
        {
            new ApiTokenValidation { Check = check }.Message.ShouldNotBeNullOrWhiteSpace();
        }
    }
}

internal sealed class ManualTime : TimeProvider
{
    private DateTimeOffset _now;

    public ManualTime(DateTimeOffset now)
    {
        _now = now;
    }

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan span) => _now += span;
}
