using FastEndpoints;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.WebApi.MetaBible.GetReaderStatus;

namespace UnshackledWord.Tooling.WebApi.Endpoints.MetaBible.GetReaderStatus;

/// <summary>Tells the reader whether any Bible text has been imported; an empty database shows a notice instead of empty chapters.</summary>
public class Endpoint : EndpointWithoutRequest<GetReaderStatusResponse>
{
    private readonly IDbReader _reader;

    public Endpoint(IDbReader reader)
    {
        _reader = reader;
    }

    public override void Configure()
    {
        Get("status");
        Group<RouteGroupConfig>();
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct) =>
        await Send.OkAsync(await ReaderStatusQuery.ReadAsync(_reader), ct);
}

public static class ReaderStatusQuery
{
    public const string HasText = """
                                  SELECT EXISTS (SELECT 1 FROM "unshackled-word"."Elb1871Verses")
                                  """;

    public static async Task<GetReaderStatusResponse> ReadAsync(IDbReader reader) =>
        new() { HasText = await reader.ExecuteScalarAsync<bool>(HasText) };
}
