namespace UnshackledWord.Tooling.SeedDb.Services.SBL.Extensions;

public static class ServiceRegistrationExtensions
{
    public static IServiceCollection AddSblServices(this IServiceCollection services)
    {
        services.AddScoped<SblTextDownloader>();
        services.AddScoped<SblApparatusDownloader>();
        services.AddScoped<SblGntTextStrategy>();
        services.AddScoped<SblGntApparatusStrategy>();
        services.AddScoped<SblRunner>();

        services.AddHttpClient("Github", client =>
        {
            client.BaseAddress = new Uri("https://github.com/");
        });

        return services;
    }
}
