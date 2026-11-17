using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UnshackledWord.Application.Features.AgentMapping;
using UnshackledWord.Application.Features.ApiTokens;
using UnshackledWord.Application.Features.ChapterOriginals;
using UnshackledWord.Application.Features.CrossReferences;
using UnshackledWord.Application.Features.Revision;
using UnshackledWord.Application.Features.Backup;
using UnshackledWord.Application.Features.StrongsOccurrences;
using UnshackledWord.Application.Features.StrongsStats;
using UnshackledWord.Application.Features.VerseMapping;
using UnshackledWord.Application.Features.VerseSearch;
using UnshackledWord.Application.Features.WordDetail;
using UnshackledWord.Application.Repositories;

namespace UnshackledWord.Infrastructure.Repositories;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        services.AddScoped<IBibleBookRepository, BibleBookRepository>();
        services.AddScoped<IElb1871TaggingRepository, Elb1871TaggingRepository>();
        services.AddScoped<IElbDashboardRepository, ElbDashboardRepository>();
        services.AddScoped<IElb1871WordRepository, Elb1871WordRepository>();
        services.AddScoped<IElb1871VerseRepository, Elb1871VerseRepository>();
        services.AddScoped<ITskRepository, TskRepository>();
        services.AddScoped<IStepBibleVerseRepository, StepBibleVerseRepository>();
        services.AddScoped<IStrongsLexiconRepository, StrongsLexiconRepository>();
        services.AddScoped<ISblTextRepository, SblTextRepository>();
        services.AddScoped<ISrWordRepository, SrWordRepository>();
        services.AddScoped<IWordDetailRepository, WordDetailRepository>();
        services.AddScoped<IVerseMappingRepository, VerseMappingRepository>();
        services.AddScoped<IRevisionRepository, RevisionRepository>();
        services.AddScoped<IRevisionService, RevisionService>();
        services.AddScoped<IRevisionCarryService, RevisionCarryService>();
        services.AddScoped<IRevisionMappingService, RevisionMappingService>();
        services.AddScoped<IRevisionWordDetailRepository, RevisionWordDetailRepository>();
        services.AddScoped<IRevisionWordDetailService, RevisionWordDetailService>();
        services.AddScoped<IHebrewMorphemeRepository, HebrewMorphemeRepository>();
        services.AddScoped<IWordDetailService, WordDetailService>();
        services.AddScoped<IVerseMappingService, VerseMappingService>();
        services.AddScoped<UnshackledWord.Application.Features.LinkRoles.Backfill.ILinkRoleBackfillRepository, LinkRoleBackfillRepository>();
        services.AddScoped<UnshackledWord.Application.Features.LinkRoles.Backfill.LinkRoleBackfillService>();
        services.AddScoped<IChapterCrossReferenceService, ChapterCrossReferenceService>();
        services.AddScoped<IChapterOriginalsService, ChapterOriginalsService>();
        services.AddScoped<IStrongsOccurrenceService, StrongsOccurrenceService>();
        services.AddScoped<IStrongsStatsRepository, StrongsStatsRepository>();
        services.AddScoped<IStrongsSummaryService, StrongsSummaryService>();
        services.AddScoped<IWordStatsService, WordStatsService>();
        services.AddScoped<ISearchOriginalsService, SearchOriginalsService>();
        services.AddScoped<IVerseSearchRepository, VerseSearchRepository>();
        services.AddScoped<IVerseSearchService, VerseSearchService>();
        services.AddScoped<ISttsTagRepository, SttsTagRepository>();
        services.AddScoped<IAgentDataRepository, AgentDataRepository>();
        services.AddScoped<IAgentStoreRepository, AgentStoreRepository>();
        services.AddScoped<IAgentPackageBuilder, AgentPackageBuilder>();
        services.AddScoped<AgentProposalStore>();
        services.AddScoped<IAgentProposalService, AgentProposalService>();
        services.AddScoped<IAgentReviewRepository, AgentReviewRepository>();
        services.AddScoped<IApiTokenRepository, ApiTokenRepository>();
        services.AddScoped<IApiTokenService, ApiTokenService>();
        services.AddScoped<IAgentRunRepository, AgentRunRepository>();
        services.AddScoped<UnshackledWord.Application.Features.AgentMapping.Scoring.IAgentScoreRepository, AgentScoreRepository>();
        services.AddScoped<UnshackledWord.Application.Features.AgentMapping.Scoring.IAgentScoreService, UnshackledWord.Application.Features.AgentMapping.Scoring.AgentScoreService>();
        services.AddScoped<UnshackledWord.Application.Features.AgentMapping.Scoring.IAgentReferenceFreezer, UnshackledWord.Application.Features.AgentMapping.Scoring.AgentReferenceFreezer>();
        services.AddSingleton<UnshackledWord.Application.Features.AgentMapping.Scoring.IAgentReferenceStore>(sp => UnshackledWord.Application.Features.AgentMapping.Scoring.AgentReferenceStore.FromConfiguration(sp.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>()));
        services.AddScoped<IAgentRunService, AgentRunService>();
        services.AddScoped<AgentRunExecutor>();
        services.AddSingleton<IAgentModelClientFactory, UnshackledWord.Infrastructure.AgentModels.AgentModelClientFactory>();
        services.AddSingleton<IAgentPromptProvider>(sp => AgentPromptProvider.FromConfiguration(sp.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>()));
        services.TryAddSingleton(TimeProvider.System);
        return services;
    }
}
