namespace UnshackledWord.Tooling.WebApi.Endpoints.Admin.Users;

/// <summary>The 409 problem details of the user administration: <c>code</c> (and for <c>email_taken</c> <c>existingUserId</c>) sit at the top level of the JSON.</summary>
public static class Conflict
{
    public static IResult Problem(string code, int? existingUserId = null)
    {
        var extensions = new Dictionary<string, object?> { ["code"] = code };

        if (existingUserId is { } id)
        {
            extensions["existingUserId"] = id;
        }

        return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: code, extensions: extensions);
    }
}
