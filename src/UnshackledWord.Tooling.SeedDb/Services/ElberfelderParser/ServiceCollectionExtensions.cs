namespace UnshackledWord.Tooling.SeedDb.Services.ElberfelderParser;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddElberfelder1871Services(this IServiceCollection services)
    {
        services.AddScoped<BibleVerseCoutingMappingStrategy>();
        services.AddScoped<Elberfelder1871Strategy>();
        services.AddScoped<Elb1871VerseRunner>();
        services.AddScoped<Elberfelder1871VerseStrategy>();
        services.AddScoped<ElberfelderTextRunner>();
        services.AddScoped<HyphenTypoDetectionService>();
        services.AddScoped<HyphenFixRunner>();
        services.AddScoped<HyphenFixUndoRunner>();
        services.AddScoped<HyphenFixVerseRunner>();
        return services;
    }
}
