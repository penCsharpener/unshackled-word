using Microsoft.Extensions.Options;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Models.Settings;
using UnshackledWord.Infrastructure.Extensions;
using UnshackledWord.Infrastructure.Services;
using UnshackledWord.Persistence.Postgres.Services;
using UnshackledWord.Tooling.SeedDb.Services;
using UnshackledWord.Tooling.SeedDb.Services.AiMappingImport;
using UnshackledWord.Tooling.SeedDb.Services.ByzTxt.Extensions;
using UnshackledWord.Tooling.SeedDb.Services.CsvImports;
using UnshackledWord.Tooling.SeedDb.Services.Elb1871WordsSrGntWordsMapper;
using UnshackledWord.Tooling.SeedDb.Services.ElberfelderParser;
using UnshackledWord.Tooling.SeedDb.Services.GlobalBibleTools;
using UnshackledWord.Tooling.SeedDb.Services.LocalOnly;
using UnshackledWord.Tooling.SeedDb.Services.OpenScriptureData;
using UnshackledWord.Tooling.SeedDb.Services.RepoSeed;
using UnshackledWord.Tooling.SeedDb.Services.SBL.Extensions;
using UnshackledWord.Tooling.SeedDb.Services.StatisticalRestorationGnt;
using UnshackledWord.Tooling.SeedDb.Services.StepBible;
using UnshackledWord.Tooling.SeedDb.Services.RevisionSeed;
using UnshackledWord.Tooling.SeedDb.Services.SttsSeed;
using UnshackledWord.Tooling.SeedDb.Services.Tsk;

namespace UnshackledWord.Tooling.SeedDb.Extensions;

public static partial class ServiceRegistrationExtensions
{
    public static IServiceCollection AddSeedDbServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddInfrastructureServices();
        services.AddSingleton(configuration);
        services.AddScoped<SeedingService>();
        services.AddScoped<SttsSeedRunner>();
        services.AddScoped<RevisionLevelSeedRunner>();
        services.AddRepoSeedServices();
        services.AddScoped<UnshackledWord.Application.Features.Revision.IRevisionRepository, UnshackledWord.Infrastructure.Repositories.RevisionRepository>(); // the realigner of the rebase verb needs it
        services.AddScoped<SrRunner>();
        services.AddScoped<TskRunner>();
        services.AddScoped<TskCurationSeedRunner>();
        services.AddScoped<GbtRunner>();
        services.AddSblServices();
        services.AddByzTxtServices();
        services.AddStepRunner();
        services.AddLocalOnlyServices();
        services.AddElberfelder1871Services();
        services.AddScoped<OpenScriptureRunner>();
        services.AddScoped<SrTxtParserStrategy>();
        services.AddScoped<SrTsvParserStrategy>();
        services.AddScoped<TskStrategy>();
        services.AddScoped<OpenScriptureHebrewStrategy>();
        services.AddScoped<GbtCsvStrategy>();
        services.AddScoped<TskTextReader>();
        services.AddAiMappingImportServices();
        services.RegisterElb1871SrWordMapperServices();
        services.RegisterCsvImportServices();
        services.AddScoped<IDbWriter, DbWriter>();
        services.AddScoped<IDbReader, DbReader>();
        services.AddSingleton<IDbConnectionFactory, PostgresDbConnectionFactory>();
        services.AddSingleton<ParseHelper>();
        services.Configure<AppSettings>(configuration.GetSection(nameof(AppSettings)));
        services.Configure<UnshackledWord.Domain.Models.Settings.VocabOptions>(configuration.GetSection(UnshackledWord.Domain.Models.Settings.VocabOptions.SectionName));
        services.Configure<UnshackledWord.Application.Features.VerseSimilarity.VerseSimilarityOptions>(configuration.GetSection(UnshackledWord.Application.Features.VerseSimilarity.VerseSimilarityOptions.SectionName));

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
        services.AddHttpClient<UnshackledWord.Tooling.SeedDb.Services.Swete.SweteDownloader>();
        services.RegisterCsvServices();

        return services;
    }

    static partial void RegisterCsvServices(this IServiceCollection services);
}
