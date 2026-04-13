using FastEndpoints;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Extensions;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Elberfelder.Search.German;

public class Endpoint : Ep.Req<Request>.Res<Dictionary<string, List<Response>>>
{
    private readonly IDbReader _reader;

    public Endpoint(IDbReader reader)
    {
        _reader = reader;
    }

    public override void Configure()
    {
        Get("search/german");
        Group<RouteGroupConfig>();
    }

    public override async Task<Dictionary<string, List<Response>>> ExecuteAsync(Request req, CancellationToken ct)
    {
        var sql = """
                  SELECT ev."HebRefId"              "RefId"
                       , ev."VerseText"             "Text"
                       , ew."PlainWord"             "ElbWord"
                       , sgw."DisambiguatedStrongs" "Strongs"
                       , sgw."Greek"
                       , sstt."Number"
                       , ssl."Gloss"                "EnglishGloss"
                       , ssl."OriginalWord"         "Lemma"
                  FROM           "unshackled-word"."Elb1871Verses" ev
                      LEFT  JOIN "unshackled-word"."Elb1871Words" ew ON ev."HebRefId" = ew."HebRefId"
                      INNER JOIN "unshackled-word"."Elb1871GreekMapping" egm ON ew."HebRefId" = egm."HebRefId" AND ew."PositionInVerse" = egm."PositionInVerse"
                      LEFT  JOIN "unshackled-word"."StepGreekWords" sgw ON egm."StepWordId" = sgw."Id"
                      INNER JOIN "unshackled-word"."StepStrongsToText" sstt ON egm."StepWordId" = sstt."StepGreekWordId" AND sstt."StepGreekWordId" IS NOT NULL
                      INNER JOIN "unshackled-word"."StepStrongsLexicon" ssl ON sstt."Number" = ssl."Number" AND ssl."LanguageId" = 2
                  WHERE 1=1
                      AND ev."SearchVector" @@ websearch_to_tsquery('german', :FtsKeyWord)
                          AND EXISTS (SELECT 1
                                      FROM UNNEST(:KeyWords) as p(pattern)
                                      WHERE ew."PlainWord" ILIKE '%' || p.pattern || '%')
                  ORDER BY ev."HebRefId"
                  """;

        var parameters = new
        {
            FtsKeyWord = req.KeyWords.Select(x => $"{x}").JoinStrings(" OR "),
            req.KeyWords
        };

        var results = (await _reader.ReadAsListAsync<Response>(sql, parameters)).ToList();
        var finalResult = new Dictionary<string, List<Response>>();

        foreach (var keyWord in req.KeyWords)
        {
            finalResult.Add(keyWord, results.Where(x => x.ElbWord.Contains(keyWord, StringComparison.OrdinalIgnoreCase)).ToList());
        }

        return finalResult;
    }
}

public sealed class Request
{
    public string[] KeyWords { get; set; } = default!;
}

public sealed class Response
{
    public int RefId { get; set; }
    public string Text { get; set; } = default!;
    public string ElbWord { get; set; } = default!;
    public string Strongs { get; set; } = default!;
    public string Greek { get; set; } = default!;
    public string EnglishGloss { get; set; } = default!;
    public string Lemma { get; set; } = default!;
}
