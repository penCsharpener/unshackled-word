using FastEndpoints;
using UnshackledWord.Application.Features.GrammarSearch;
using UnshackledWord.Domain.WebApi.Elberfelder.SearchGrammar;
using UnshackledWord.Tooling.WebApi.RateLimiting;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Elberfelder.Search.Grammar.Vocabulary;

/// <summary>Keys, aliases and values of the grammar query (roadmap 0029.3); public read in the search rate-limit group, static per deployment.</summary>
[RateLimitGroup(RateLimitGroups.Search)]
public class Endpoint : EndpointWithoutRequest<GrammarVocabularyResponse>
{
    public override void Configure()
    {
        Get("search/grammar/vocabulary");
        Group<RouteGroupConfig>();
        AllowAnonymous();
        Description(b => b.Produces<GrammarVocabularyResponse>(200));
        Summary(s => s.Summary = "The grammar query vocabulary: keys with aliases, languages and kind (filter or word), and per key its values with German and English label. Code, Strong and Lemma have free values (freeValue, no value list). Cached for a day.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        HttpContext.Response.Headers.CacheControl = "public, max-age=86400";
        await Send.OkAsync(GrammarVocabularyContract.Build(), ct);
    }
}
