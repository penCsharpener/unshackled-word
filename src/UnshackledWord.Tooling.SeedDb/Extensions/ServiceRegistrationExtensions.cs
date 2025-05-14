using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Models.Settings;
using UnshackledWord.Infrastructure.Extensions;
using UnshackledWord.Infrastructure.Services;
using UnshackledWord.Persistence.Postgres.Services;
using UnshackledWord.Tooling.SeedDb.Services;
using UnshackledWord.Tooling.SeedDb.Services.Abstractions;
using UnshackledWord.Tooling.SeedDb.Services.BibelKommentare;
using UnshackledWord.Tooling.SeedDb.Services.ElberfelderParser;
using UnshackledWord.Tooling.SeedDb.Services.EliranWongData;
using UnshackledWord.Tooling.SeedDb.Services.StatisticalRestorationGnt;

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
        builder.Services.AddScoped<Elberfelder1871Strategy>();
        builder.Services.AddScoped<RalfsLxxParserStrategy>();
        builder.Services.AddScoped<ElberfelderMergeStrategy>();
        builder.Services.AddScoped<SrRunner>();
        builder.Services.AddScoped<ElbRunner>();
        builder.Services.AddScoped<BkRunner>();
        builder.Services.AddScoped<IDbWriter, DbWriter>();
        builder.Services.AddScoped<IDbReader, DbReader>();
        builder.Services.AddSingleton<IDbConnectionFactory, PostgresDbConnectionFactory>();
        builder.Services.AddSingleton<ParseHelper>();
        builder.Services.Configure<AppSettings>(builder.Configuration.GetSection(nameof(AppSettings)));

        builder.Services.AddHttpClient<GithubFileDownloader>(client =>
        {
            client.BaseAddress = new Uri("https://github.com/");
        });
        // builder.Services.AddHttpClient<IFileDownloader, BibelKommentareDownloader>(client => {
        //     client.BaseAddress = new Uri("https://www.bibelkommentare.de/");
        // });
        builder.Services.AddScoped<IFileDownloader, BibelKommentareCopyService>();
    }
}
