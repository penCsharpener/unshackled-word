using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using UnshackledWord.Tooling.WebApi.Endpoints.Admin.Users;

namespace UnshackledWord.Tooling.WebApi.Tests;

/// <summary>The frontend reads <c>code</c> and <c>existingUserId</c> from the top level of the 409 body.</summary>
public class UserAdminConflictTests
{
    private static async Task<(int Status, string? ContentType, JsonElement Body)> RenderAsync(IResult result)
    {
        var context = new DefaultHttpContext { RequestServices = new ServiceCollection().AddLogging().AddOptions().BuildServiceProvider() };
        context.Response.Body = new MemoryStream();

        await result.ExecuteAsync(context);

        context.Response.Body.Position = 0;
        var body = await JsonDocument.ParseAsync(context.Response.Body);
        return (context.Response.StatusCode, context.Response.ContentType, body.RootElement.Clone());
    }

    [Fact]
    public async Task Problem_WritesCodeAtTopLevel_AsProblemJson()
    {
        var (status, contentType, body) = await RenderAsync(Conflict.Problem("last_admin"));

        status.ShouldBe(409);
        contentType.ShouldContain("application/problem+json");
        body.GetProperty("code").GetString().ShouldBe("last_admin");
        body.TryGetProperty("existingUserId", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Problem_EmailTaken_CarriesNumericExistingUserId()
    {
        var (_, _, body) = await RenderAsync(Conflict.Problem("email_taken", 42));

        body.GetProperty("code").GetString().ShouldBe("email_taken");
        body.GetProperty("existingUserId").GetInt32().ShouldBe(42);
    }
}
