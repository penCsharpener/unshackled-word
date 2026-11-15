using UnshackledWord.Tooling.WebApi.RateLimiting;

namespace UnshackledWord.Tooling.WebApi.Tests.RateLimiting;

public class RateLimitingOptionsTests
{
    private static readonly RateLimitingOptionsValidator Validator = new();

    private static RateLimitingOptions Valid() => new() { Enabled = true };

    [Fact]
    public void Defaults_AreValid()
    {
        Validator.Validate(null, Valid()).Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Disabled_SkipsValidation()
    {
        var options = new RateLimitingOptions { Enabled = false, General = new() { PermitLimit = 0, WindowSeconds = 0, SegmentsPerWindow = 0 } };

        Validator.Validate(null, options).Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void ZeroPermitLimit_Fails_WithAMessageNamingTheSetting()
    {
        var options = Valid();
        options.General.PermitLimit = 0;

        var result = Validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("RateLimiting:General:PermitLimit");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3601)]
    public void WindowSeconds_OutOfRange_Fails(int seconds)
    {
        var options = Valid();
        options.Strict.WindowSeconds = seconds;

        Validator.Validate(null, options).FailureMessage!.ShouldContain("RateLimiting:Strict:WindowSeconds");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(61)]
    public void SegmentsPerWindow_OutOfRange_Fails(int segments)
    {
        var options = Valid();
        options.General.WindowSeconds = 60;
        options.General.SegmentsPerWindow = segments;

        Validator.Validate(null, options).FailureMessage!.ShouldContain("RateLimiting:General:SegmentsPerWindow");
    }
}

public class ReverseProxyOptionsTests
{
    [Fact]
    public void EmptyAndBlankEntries_AreIgnored()
    {
        var options = new ReverseProxyOptions { KnownNetworks = ["", "  "], KnownProxies = [""] };

        options.HasEntries.ShouldBeFalse();
    }

    [Fact]
    public void ConfiguredEntries_ReplaceTheLoopbackDefaults()
    {
        var options = new ReverseProxyOptions { KnownNetworks = ["", "172.16.0.0/12"], KnownProxies = ["10.0.0.5"] };

        var forwarded = options.ToForwardedHeadersOptions();

        options.HasEntries.ShouldBeTrue();
        forwarded.KnownProxies.Select(p => p.ToString()).ShouldBe(["10.0.0.5"]);
        forwarded.KnownIPNetworks.Count.ShouldBe(1);
        forwarded.ForwardLimit.ShouldBe(1);
    }

    [Fact]
    public void InvalidNetwork_FailsWithAMessage()
    {
        var options = new ReverseProxyOptions { KnownNetworks = ["not-a-network"] };

        Should.Throw<InvalidOperationException>(() => options.ToForwardedHeadersOptions()).Message.ShouldContain("not-a-network");
    }
}
