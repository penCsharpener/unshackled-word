namespace UnshackledWord.Tooling.SeedDb.Services.ByzTxt.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddByzTxtServices(this IServiceCollection services)
    {
        services.AddScoped<ByzRunner>();
        services.AddScoped<ByzTxtStrategy>();
        services.AddScoped<ByzRunner>();

        services.AddHttpClient("Github", client =>
        {
            client.BaseAddress = new Uri("https://github.com/");
        });

        return services;
    }
}
