using FastEndpoints;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Extensions;
using UnshackledWord.Domain.WebApi;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Elberfelder.Fts.GetElbWordOccurrence;

public class Endpoint : Ep.Req<Request>.Res<PaginationResult<Response>>
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

    public override async Task<PaginationResult<Response>> ExecuteAsync(Request req, CancellationToken ct)
    {
        var parameters = new
        {
            req.SearchTerm,
            LikeSearchTerm = $"%{req.SearchTerm}%",
            req.PageSize,
            Offset = (req.Page - 1) * req.PageSize
        };

        var sqlFrom = """
                      FROM "unshackled-word"."Elb1871Verses" ev
                          INNER JOIN "unshackled-word"."Elb1871Words" ew ON ev."HebRefId" = ew."HebRefId"
                          LEFT JOIN "unshackled-word"."Elb1871GreekMapping" egm ON ew."Id" = egm."ElbWordId"
                          INNER JOIN "unshackled-word"."StepGreekWords" sgw ON egm."StepWordId" = sgw."Id"
                          INNER JOIN "unshackled-word"."StepStrongsToText" sstt ON sstt."StepGreekWordId" = sgw."Id"
                      WHERE ev."SearchVector" @@ websearch_to_tsquery('german', @SearchTerm)
                          AND ew."PlainWord" ILIKE @LikeSearchTerm
                      """;

        var sql = $"""
                   SELECT ev."HebRefId" "RefId"
                        , ev."VerseText"
                        , ew."PlainWord"
                        , sgw."DisambiguatedStrongs" "Strongs"
                        , sgw."Greek"
                        , sgw."GreekNoDiacritics"
                        , sgw."Id"
                        , sstt."StepGreekWordId"
                        , sstt."Number"
                   {sqlFrom}
                   ORDER BY ev."HebRefId"
                   LIMIT {parameters.PageSize} OFFSET {parameters.Offset};
                   """;

         var sqlCount = $"""
                         SELECT COUNT(*)
                         {sqlFrom}
                         """;

        var results = await _reader.ReadAsListAsync<Response>(sql, parameters);
        var count = await _reader.ExecuteScalarAsync<int>(sqlCount, parameters);

        return new PaginationResult<Response>(results.ToList(), count, req.PageSize);
    }
}

public sealed class Request : PaginationRequest
{
    public string SearchTerm { get; set; } = default!;
}

public sealed class Response
{
    public int RefId { get; set; }
    public string VerseText { get; set; } = default!;
    public string PlainWord { get; set; } = default!;
    public string Greek { get; set; } = default!;
    public string GreekNoDiacritics { get; set; } = default!;
    public string Strongs { get; set; } = default!;
}
