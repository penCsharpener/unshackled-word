using System.Net;
using UnshackledWord.Tooling.WebApi.RateLimiting;

namespace UnshackledWord.Tooling.WebApi.Tests.RateLimiting;

public class RateLimitPartitionerTests
{
    [Fact]
    public void GeneralKey_Anonymous_UsesGeneralAndIp()
    {
        RateLimitPartitioner.GeneralKey(false, IPAddress.Parse("203.0.113.7")).ShouldBe("general:203.0.113.7");
    }

    [Fact]
    public void GeneralKey_Authenticated_HasNoLimit()
    {
        RateLimitPartitioner.GeneralKey(true, IPAddress.Parse("203.0.113.7")).ShouldBeNull();
    }

    [Fact]
    public void GeneralKey_WithoutIp_SharesOneUnknownBucket()
    {
        RateLimitPartitioner.GeneralKey(false, null).ShouldBe("general:unknown");
    }

    [Fact]
    public void ClientKey_MappedIPv4_IsUnwrapped()
    {
        RateLimitPartitioner.ClientKey(IPAddress.Parse("::ffff:203.0.113.7")).ShouldBe("203.0.113.7");
    }

    [Fact]
    public void ClientKey_IPv6_GroupsBySlash64()
    {
        var a = RateLimitPartitioner.ClientKey(IPAddress.Parse("2001:db8:1:2:aaaa:bbbb:cccc:dddd"));
        var b = RateLimitPartitioner.ClientKey(IPAddress.Parse("2001:db8:1:2::1"));
        var other = RateLimitPartitioner.ClientKey(IPAddress.Parse("2001:db8:1:3::1"));

        a.ShouldBe(b);
        a.ShouldNotBe(other);
        a.ShouldEndWith("/64");
    }

    [Fact]
    public void StrictKey_AnonymousInGroup_UsesGroupAndIp()
    {
        RateLimitPartitioner.StrictKey(false, RateLimitGroups.Search, IPAddress.Parse("203.0.113.7")).ShouldBe("search:203.0.113.7");
    }

    [Fact]
    public void StrictKey_GroupsHaveSeparateKeys()
    {
        var ip = IPAddress.Parse("203.0.113.7");

        RateLimitPartitioner.StrictKey(false, RateLimitGroups.Auth, ip).ShouldNotBe(RateLimitPartitioner.StrictKey(false, RateLimitGroups.Search, ip));
    }

    [Theory]
    [InlineData(true, "search")]
    [InlineData(false, null)]
    [InlineData(false, "")]
    public void StrictKey_AuthenticatedOrWithoutGroup_HasNoLimit(bool isAuthenticated, string? group)
    {
        RateLimitPartitioner.StrictKey(isAuthenticated, group, IPAddress.Parse("203.0.113.7")).ShouldBeNull();
    }

    [Theory]
    [InlineData("203.0.113.7", "203.0.113.0")]
    [InlineData("::ffff:203.0.113.7", "203.0.113.0")]
    public void ShortenForLog_ZeroesLastIPv4Octet(string ip, string expected)
    {
        RateLimitPartitioner.ShortenForLog(IPAddress.Parse(ip)).ShouldBe(expected);
    }

    [Fact]
    public void ShortenForLog_IPv6_IsThePrefix()
    {
        RateLimitPartitioner.ShortenForLog(IPAddress.Parse("2001:db8:1:2:aaaa:bbbb:cccc:dddd")).ShouldBe("2001:db8:1:2::/64");
    }
}
