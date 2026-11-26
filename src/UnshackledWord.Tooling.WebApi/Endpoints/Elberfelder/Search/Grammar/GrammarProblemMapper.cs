using UnshackledWord.Application.Features.GrammarSearch;
using UnshackledWord.Domain.WebApi.Elberfelder.SearchGrammar;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Elberfelder.Search.Grammar;

/// <summary>The 400 body of a query that cannot be searched, shared by the grammar search endpoints.</summary>
public static class GrammarProblemMapper
{
    public static GrammarSearchProblem ToProblem(GrammarQueryError error) => new()
    {
        Code = error.Code,
        Token = error.Token,
        Candidates = error.Candidates is null ? null : [.. error.Candidates],
    };
}
