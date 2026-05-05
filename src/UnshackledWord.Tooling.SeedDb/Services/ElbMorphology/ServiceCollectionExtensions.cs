namespace UnshackledWord.Tooling.SeedDb.Services.ElbMorphology;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddElbMorphology(this IServiceCollection services)
    {
        services.AddScoped<ElbMorphologyRunner>();

        return services;
    }
}