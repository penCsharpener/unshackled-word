using Microsoft.Extensions.Options;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Models.Settings;
using UnshackledWord.Infrastructure.Extensions;
using UnshackledWord.Infrastructure.Services;
using UnshackledWord.Persistence.Postgres.Services;
using UnshackledWord.Tooling.SeedDb.Services;
using UnshackledWord.Tooling.SeedDb.Services.Abstractions;
using UnshackledWord.Tooling.SeedDb.Services.BibelKommentare;
using UnshackledWord.Tooling.SeedDb.Services.ByzTxt.Extensions;
using UnshackledWord.Tooling.SeedDb.Services.Elb1871Lemmatizer;
using UnshackledWord.Tooling.SeedDb.Services.Elb1871WordsSrGntWordsMapper;
using UnshackledWord.Tooling.SeedDb.Services.ElberfelderParser;
using UnshackledWord.Tooling.SeedDb.Services.EliranWongData;
using UnshackledWord.Tooling.SeedDb.Services.GlobalBibleTools;
using UnshackledWord.Tooling.SeedDb.Services.OpenScriptureData;
using UnshackledWord.Tooling.SeedDb.Services.SBL;
using UnshackledWord.Tooling.SeedDb.Services.SBL.Extensions;
using UnshackledWord.Tooling.SeedDb.Services.StatisticalRestorationGnt;
using UnshackledWord.Tooling.SeedDb.Services.Tsk;

namespace UnshackledWord.Tooling.SeedDb.Extensions;

public static class ServiceRegistrationExtensions
{
    public static IServiceCollection AddSeedDbServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddInfrastructureServices();
        services.AddSingleton(configuration);
        services.AddScoped<SeedingService>();
        services.AddScoped<IFileParserFactory, FileParserFactory>();
        services.AddScoped<ElberfelderTextRunner>();
        services.AddScoped<SrRunner>();
        services.AddScoped<ElbRunner>();
        services.AddScoped<BkRunner>();
        services.AddScoped<TskRunner>();
        services.AddScoped<GbtRunner>();
        services.AddSblServices();
        services.AddByzTxtServices();
        services.AddElberfelder1871Lemmatizer();
        services.AddScoped<OpenScriptureRunner>();
        services.AddScoped<SrTxtParserStrategy>();
        services.AddScoped<SrTsvParserStrategy>();
        services.AddScoped<ElbParserStrategy>();
        services.AddScoped<Elberfelder1871Strategy>();
        services.AddScoped<ElberfelderMergeStrategy>();
        services.AddScoped<RalfsLxxParserStrategy>();
        services.AddScoped<TskStrategy>();
        services.AddScoped<OpenScriptureHebrewStrategy>();
        services.AddScoped<GbtCsvStrategy>();
        services.AddScoped<TskTextReader>();
        services.RegisterElb1871SrWordMapperServices();
        services.AddScoped<IDbWriter, DbWriter>();
        services.AddScoped<IDbReader, DbReader>();
        services.AddSingleton<IDbConnectionFactory, PostgresDbConnectionFactory>();
        services.AddSingleton<ParseHelper>();
        services.Configure<AppSettings>(configuration.GetSection(nameof(AppSettings)));

        services.AddHttpClient<GithubFileDownloader>(client =>
        {
            client.BaseAddress = new Uri("https://github.com/");
        });
        services.AddHttpClient<OpenScriptureHebrewDownloader>((sp, client) =>
        {
            var url = sp.GetRequiredService<IOptions<AppSettings>>().Value.DatabaseSeeding.OpenScripturesGithub
                .DownloadDomain;
            client.BaseAddress = new Uri(url);
        });
        // services.AddHttpClient<IFileDownloader, BibelKommentareDownloader>(client => {
        //     client.BaseAddress = new Uri("https://www.bibelkommentare.de/");
        // });
        services.AddScoped<IFileDownloader, BibelKommentareCopyService>();
        services.RegisterCsvServices();

        return services;
    }
}
