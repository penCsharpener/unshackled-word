namespace UnshackledWord.Tooling.SeedDb.Services.Elb1871WordsSrGntWordsMapper;

public static class ServiceRegistrationExtensions
{
    public static IServiceCollection RegisterElb1871SrWordMapperServices(this IServiceCollection services)
    {
        services.AddScoped<Elb1871SrMappingRunner>();
        services.AddScoped<Elb1871SrGntStrategy>();
        services.AddScoped<Elb1871SrGntRepository>();
        services.AddScoped<MappingFileReader>();

        return services;
    }
}
