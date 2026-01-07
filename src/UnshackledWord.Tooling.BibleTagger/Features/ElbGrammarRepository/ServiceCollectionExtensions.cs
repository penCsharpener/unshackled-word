namespace UnshackledWord.Tooling.BibleTagger.Features.ElbGrammarRepository;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddElbGrammar(this IServiceCollection services)
    {
        services.AddScoped<IElbGrammarRepository, ElbGrammarRepository>();

        return services;
    }
}
