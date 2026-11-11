using Microsoft.Extensions.Options;
using UnshackledWord.Application.Features.Auth;

namespace UnshackledWord.Application.Tests.Features.Auth;

public class AdmissionCheckTests
{
    private readonly IAdmissionRepository _repository = Substitute.For<IAdmissionRepository>();
    private readonly TimeProvider _time = TimeProvider.System;

    private AdmissionCheck Create(RegistrationOptions options) => new(Options.Create(options), _repository, _time);

    private static RegistrationOptions Restricted(string[]? domains = null, string[]? emails = null) => new()
    {
        Mode = RegistrationMode.Restricted,
        AllowedEmailDomains = domains ?? [],
        AllowedEmails = emails ?? [],
    };

    [Fact]
    public async Task OpenMode_AdmitsEveryone()
    {
        var outcome = await Create(new RegistrationOptions()).CheckAsync("a@b.de", null, default);

        outcome.IsAdmitted.ShouldBeTrue();
        outcome.ViaInviteCode.ShouldBeFalse();
    }

    [Fact]
    public async Task Restricted_FirstAccountIsAdmitted()
    {
        _repository.CountUsersAsync(Arg.Any<CancellationToken>()).Returns(0L);

        var outcome = await Create(Restricted()).CheckAsync("a@b.de", null, default);

        outcome.IsAdmitted.ShouldBeTrue();
    }

    [Fact]
    public async Task Restricted_AllowedDomainIsAdmittedWithoutCode()
    {
        _repository.CountUsersAsync(Arg.Any<CancellationToken>()).Returns(3L);

        var outcome = await Create(Restricted(domains: ["school.example"])).CheckAsync("a@school.example", null, default);

        outcome.IsAdmitted.ShouldBeTrue();
        outcome.ViaInviteCode.ShouldBeFalse();
    }

    [Fact]
    public async Task Restricted_WithoutCode_IsRefusedOnEmail()
    {
        _repository.CountUsersAsync(Arg.Any<CancellationToken>()).Returns(3L);

        var outcome = await Create(Restricted()).CheckAsync("a@b.de", null, default);

        outcome.IsAdmitted.ShouldBeFalse();
        outcome.RefusalAttributedToInviteCode.ShouldBeFalse();
    }

    [Fact]
    public async Task Restricted_UnknownCode_IsRefusedOnCode()
    {
        _repository.CountUsersAsync(Arg.Any<CancellationToken>()).Returns(3L);
        _repository.FindInviteCodeAsync("ABC", Arg.Any<CancellationToken>()).Returns((InviteCodeCheckRow?)null);

        var outcome = await Create(Restricted()).CheckAsync("a@b.de", "ABC", default);

        outcome.IsAdmitted.ShouldBeFalse();
        outcome.RefusalAttributedToInviteCode.ShouldBeTrue();
    }

    [Fact]
    public async Task Restricted_ValidCode_IsAdmittedViaInviteCode()
    {
        _repository.CountUsersAsync(Arg.Any<CancellationToken>()).Returns(3L);
        _repository.FindInviteCodeAsync("ABC", Arg.Any<CancellationToken>()).Returns(new InviteCodeCheckRow
        {
            MaxSeats = 2,
            SeatsUsed = 1,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(1),
        });

        var outcome = await Create(Restricted()).CheckAsync("a@b.de", "ABC", default);

        outcome.IsAdmitted.ShouldBeTrue();
        outcome.ViaInviteCode.ShouldBeTrue();
    }

    [Theory]
    [InlineData(true, false, false, false)] // revoked
    [InlineData(false, true, false, false)] // expired
    [InlineData(false, false, true, false)] // used up
    [InlineData(false, false, false, true)] // bound to another address
    public async Task Restricted_InvalidCode_IsRefused(bool revoked, bool expired, bool usedUp, bool otherEmail)
    {
        _repository.CountUsersAsync(Arg.Any<CancellationToken>()).Returns(3L);
        _repository.FindInviteCodeAsync("ABC", Arg.Any<CancellationToken>()).Returns(new InviteCodeCheckRow
        {
            MaxSeats = 1,
            SeatsUsed = usedUp ? 1 : 0,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(expired ? -1 : 1),
            RevokedAt = revoked ? DateTimeOffset.UtcNow : null,
            BoundEmailNormalized = otherEmail ? "someone@else.de" : null,
        });

        var outcome = await Create(Restricted()).CheckAsync("a@b.de", "ABC", default);

        outcome.IsAdmitted.ShouldBeFalse();
        outcome.RefusalAttributedToInviteCode.ShouldBeTrue();
    }

    [Fact]
    public void EmailFilter_MatchesWholeDomainNotSuffix()
    {
        var options = Restricted(domains: ["example.com"]);

        EmailFilter.IsAllowed("a@example.com", options).ShouldBeTrue();
        EmailFilter.IsAllowed("a@notexample.com", options).ShouldBeFalse();
    }

    [Fact]
    public void Normalizers_ProduceCanonicalForms()
    {
        EmailNormalizer.Normalize("  Reader@Example.COM ").ShouldBe("reader@example.com");
        InviteCodeNormalizer.Normalize(" a1b2-c3 d4 ").ShouldBe("A1B2C3D4");
        AuthTokenHasher.Hash("x").ShouldBe(AuthTokenHasher.Hash("x"));
        AuthTokenHasher.Hash("x").ShouldNotBe(AuthTokenHasher.Hash("y"));
    }
}
