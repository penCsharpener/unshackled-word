using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Models.Settings;
using UnshackledWord.Infrastructure.Extensions;
using UnshackledWord.Tooling.SeedDb.Services;
using UnshackledWord.Tooling.SeedDb.Services.Abstractions;
using UnshackledWord.Tooling.SeedDb.Services.ElberfelderParser;

namespace UnshackledWord.Tooling.SeedDb.Extensions;

public static class ServiceRegistrationExtensions
{
    public static void AddSeedDbServices(this HostApplicationBuilder builder)
    {
        builder.Services.AddInfrastructureServices();
        builder.Services.AddScoped<SeedingService>();
        builder.Services.AddScoped<IFileParserFactory, FileParserFactory>();
        builder.Services.AddScoped<SrTxtParserStrategy>();
        builder.Services.AddScoped<SrTsvParserStrategy>();
        builder.Services.AddScoped<ElbParserStrategy>();
        builder.Services.AddSingleton<ParseHelper>();
        builder.Services.Configure<AppSettings>(builder.Configuration.GetSection(nameof(AppSettings)));

        builder.Services.AddHttpClient<GithubFileDownloader>(client =>
        {
            client.BaseAddress = new Uri("https://github.com/");
        });
        // builder.Services.AddHttpClient<BibelKommentareDownloader>(client => {
        //     client.BaseAddress = new Uri("https://www.bibelkommentare.de/");
        // });
        builder.Services.AddScoped<IFileDownloader, BibelKommentareCopyService>();
    }
}
