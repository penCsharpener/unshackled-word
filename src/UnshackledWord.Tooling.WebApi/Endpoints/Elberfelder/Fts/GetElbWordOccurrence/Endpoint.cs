using FastEndpoints;
using UnshackledWord.Application.Abstractions;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Elberfelder.Fts.GetElbWordOccurrence;

public class Endpoint : Ep.Req<Request>.Res<List<Response>>
{
    private readonly IDbReader _reader;

    public Endpoint(IDbReader reader)
    {
        _reader = reader;
    }

    public override void Configure()
    {
        Get("fts/single");
        Group<RouteGroupConfig>();
    }

    public override async Task<List<Response>> ExecuteAsync(Request req, CancellationToken ct)
    {
        var sql = """
                  SELECT ev."HebRefId" "RefId"
                       , ev."VerseText"
                       , ew."PlainWord"
                       , sgw."DisambiguatedStrongs" "Strongs"
                       , sgw."Greek"
                       , sgw."Id"
                       , sstt."StepGreekWordId"
                       , sstt."Number"
                  FROM "unshackled-word"."Elb1871Verses" ev
                      INNER JOIN "unshackled-word"."Elb1871Words" ew ON ev."HebRefId" = ew."HebRefId"
                      LEFT JOIN "unshackled-word"."Elb1871GreekMapping" egm ON ew."Id" = egm."ElbWordId"
                      INNER JOIN "unshackled-word"."StepGreekWords" sgw ON egm."StepWordId" = sgw."Id"
                      INNER JOIN "unshackled-word"."StepStrongsToText" sstt ON sstt."StepGreekWordId" = sgw."Id"
                  WHERE "SearchVector" @@ websearch_to_tsquery('german', $SearchItem)
                      AND ew."PlainWord" LIKE $LikeSearchTerm
                  """;

        var parameters = new
        {
            req.SearchTerm,
            LikeSearchTerm = $"%{req.SearchTerm}%"
        };

        return (await _reader.ReadAsListAsync<Response>(sql, parameters)).ToList();
    }
}

public sealed class Request
{
    public string SearchTerm { get; set; } = default!;
}

public sealed class Response
{
    public int RefId { get; set; }
    public string VerseText { get; set; } = default!;
    public string PlainWord { get; set; } = default!;
    public string Greek { get; set; } = default!;
    public string Strongs { get; set; } = default!;
}
