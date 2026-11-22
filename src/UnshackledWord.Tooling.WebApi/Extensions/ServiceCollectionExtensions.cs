using Microsoft.Extensions.DependencyInjection.Extensions;
using UnshackledWord.Infrastructure.Extensions;
using UnshackledWord.Infrastructure.Repositories;
using UnshackledWord.Infrastructure.Seed;
using UnshackledWord.Persistence.Postgres.Extensions;
using UnshackledWord.Tooling.WebApi.Endpoints.BibleTagger.BackupElbData;
using UnshackledWord.Tooling.WebApi.Endpoints.BibleTagger.BackupElbData.Mappings;
using UnshackledWord.Tooling.WebApi.Models;

namespace UnshackledWord.Tooling.WebApi.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddWebApiServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AppSettings>(configuration.GetSection(nameof(AppSettings)));
        // The seed importers (shared with the SeedDb CLI) read the same section through the domain settings class.
        services.Configure<UnshackledWord.Domain.Models.Settings.AppSettings>(configuration.GetSection(nameof(AppSettings)));
        services.Configure<UnshackledWord.Domain.Models.Settings.VocabOptions>(configuration.GetSection(UnshackledWord.Domain.Models.Settings.VocabOptions.SectionName));
        services.AddPostgresPersistence();
        services.AddInfrastructureServices();
        services.AddRepositories();
        services.AddSeedImportServices();
        services.AddApiServices();

        return services;
    }

    public static IServiceCollection AddApiServices(this IServiceCollection services)
    {
        services.AddScoped<BackupFileService>();
        services.AddScoped<Endpoints.Lemmas.ILemmaRepository, Endpoints.Lemmas.LemmaRepository>();
        services.AddScoped<Endpoints.Lemmas.ILemmaBulkRepository, Endpoints.Lemmas.LemmaBulkRepository>();
        services.AddScoped<Endpoints.Lemmas.Stts.LemmaSttsGuard>();
        services.AddScoped<Endpoints.Revision.IRevisionLevelRepository, Endpoints.Revision.RevisionLevelRepository>();
        services.AddScoped<Endpoints.Revision.RevisionLevelService>();
        services.AddScoped<Endpoints.Revision.IRevisionOverviewRepository, Endpoints.Revision.RevisionOverviewRepository>();
        services.AddScoped<Endpoints.Revision.RevisionOverviewService>();
        services.AddScoped<Endpoints.Revision.BulkReplaceSearch.IBulkReplaceSearchRepository, Endpoints.Revision.BulkReplaceSearch.BulkReplaceSearchRepository>();
        services.AddScoped<Endpoints.Revision.BulkReplaceSearch.BulkReplaceSearchService>();
        services.AddHostedService<Endpoints.Lemmas.Stts.SttsDriftCheck>();
        services.AddSingleton<Endpoints.DataQuality.StrongsLexiconIndex>();
        services.AddSingleton<Endpoints.Elberfelder.Search.SourceWords.SourceFormIndex>();
        services.AddScoped<Endpoints.Elberfelder.Search.SourceWords.ISourceWordRefs, Endpoints.Elberfelder.Search.SourceWords.SourceWordRefs>();
        services.AddScoped<Endpoints.Elberfelder.Search.SourceWords.ISourceWordSearchService, Endpoints.Elberfelder.Search.SourceWords.SourceWordSearchService>();
        services.AddSingleton<Endpoints.DataQuality.DqRefreshCoordinator>();
        services.AddSingleton<UnshackledWord.Application.Features.StrongsStats.IStatsRefreshStatus>(sp => sp.GetRequiredService<Endpoints.DataQuality.DqRefreshCoordinator>());
        services.AddScoped<Endpoints.DataQuality.IDqFactsRepository, Endpoints.DataQuality.DqFactsRepository>();
        services.AddScoped<Endpoints.DataQuality.IDqCheckRepository, Endpoints.DataQuality.DqCheckRepository>();
        services.AddScoped<Endpoints.DataQuality.IDqExplorerRepository, Endpoints.DataQuality.DqExplorerRepository>();
        services.AddScoped<Endpoints.DataQuality.DqOverviewService>();
        services.AddSingleton<Endpoints.Maintainer.ExportSeed.SeedExportCoordinator>();
        services.AddScoped<Endpoints.Maintainer.ExportSeed.SeedZipBuilder>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<Endpoints.Maintainer.SeedAdmin.SeedJobRunner>();
        services.AddScoped<UnshackledWord.Infrastructure.Vocab.VocabCourseRepository>();
        services.AddScoped<UnshackledWord.Infrastructure.Vocab.VocabEntryRepository>();
        services.AddScoped<UnshackledWord.Infrastructure.Vocab.VocabEditorRepository>();
        services.AddScoped<UnshackledWord.Infrastructure.Vocab.VocabEntryEditor>();
        // Study sessions (roadmap 0022.8): the FSRS wrapper holds only its parameters, so one instance serves everyone.
        services.AddSingleton<UnshackledWord.Application.Features.Vocab.VocabScheduler>();
        services.AddScoped<UnshackledWord.Infrastructure.Vocab.VocabSessionService>();
        services.AddScoped<UnshackledWord.Infrastructure.Vocab.VocabChangeService>();
        services.AddScoped<UnshackledWord.Infrastructure.Vocab.VocabSettingsService>();
        services.AddSingleton<Endpoints.Maintainer.LinkRoles.LinkRoleJobRunner>();
        // Agent runs (roadmap 0016.6): validates the provider profiles at startup and runs the background loop of started runs.
        services.AddSingleton<Endpoints.Maintainer.AgentRuns.AgentRunManager>();
        services.AddHostedService(sp => sp.GetRequiredService<Endpoints.Maintainer.AgentRuns.AgentRunManager>());
        services.AddScoped<Endpoints.Maintainer.SeedAdmin.SeedOperationService>();
        // Proposal review (roadmap 0016.7): the merge needs the lemma editor's repository, which lives in this project.
        services.AddScoped<UnshackledWord.Application.Features.AgentMapping.IAgentLemmaWriter, Endpoints.Maintainer.AgentReview.AgentLemmaWriter>();
        services.AddScoped<UnshackledWord.Application.Features.AgentMapping.IAgentReviewService, UnshackledWord.Application.Features.AgentMapping.AgentReviewService>();
        return services;
    }
}
