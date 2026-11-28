namespace UnshackledWord.Seeding.ElberfelderParser;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddElberfelder1871Services(this IServiceCollection services)
    {
        services.AddScoped<BibleVerseCoutingMappingStrategy>();
        services.AddScoped<Elb1871VerseRunner>();
        services.AddScoped<ElberfelderTextRunner>();
        services.AddScoped<HyphenTypoDetectionService>();
        services.AddScoped<HyphenFixRunner>();
        services.AddScoped<HyphenFixVerseRunner>();
        return services;
    }
}
