using Microsoft.Extensions.DependencyInjection;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Infrastructure.Services;

namespace UnshackledWord.Infrastructure.Extensions;

public static class ServiceRegistrationExtensions
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        services.AddSingleton<IFileService, FileService>();
        services.AddSingleton<IUnzipService, UnzipService>();
        services.AddHttpClient<IFileDownloader, GithubFileDownloader>(client =>
        {
            client.BaseAddress = new Uri("https://github.com/");
        });
        return services;
    }
}
