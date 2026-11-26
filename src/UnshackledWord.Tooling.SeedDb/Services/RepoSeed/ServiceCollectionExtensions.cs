using UnshackledWord.Infrastructure.Seed;
using UnshackledWord.Tooling.SeedDb.Services.RepoSeed.Morphology;
using UnshackledWord.Tooling.SeedDb.Services.RepoSeed.Verbs;
using UnshackledWord.Tooling.SeedDb.Services.RevisionSeed;
using UnshackledWord.Tooling.SeedDb.Services.StepBible;

namespace UnshackledWord.Tooling.SeedDb.Services.RepoSeed;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRepoSeedServices(this IServiceCollection services)
    {
        services.AddSeedImportServices();
        services.AddScoped<MorphologySeedRunner>();
        services.AddScoped<RevisionSeedRunner>();
        services.AddScoped<ISeedVerb, ExportSeedVerb>();
        services.AddScoped<ISeedVerb, RebaseVerb>();
        services.AddScoped<ISeedVerb, AnchorCheckVerb>();
        services.AddScoped<ISeedVerb, VocabGenerateVerb>();
        services.AddScoped<ISeedVerb, VocabDeriveVerb>();
        services.AddScoped<StepLexiconRepairService>();
        services.AddScoped<ISeedVerb, StepLexiconRepairVerb>();
        services.AddScoped<UnshackledWord.Application.Features.VerseSimilarity.IVerseTokenRepository, UnshackledWord.Infrastructure.VerseSimilarity.VerseTokenRepository>();
        services.AddScoped<UnshackledWord.Application.Features.VerseSimilarity.IVerseSimilarityRepository, UnshackledWord.Infrastructure.VerseSimilarity.VerseSimilarityRepository>();
        services.AddScoped<UnshackledWord.Application.Features.VerseSimilarity.VerseSimilarityRebuildService>();
        services.AddScoped<ISeedVerb, VerseSimilarityVerb>();
        services.AddScoped<UnshackledWord.Tooling.SeedDb.Services.Swete.LxxSourceLoader>();
        services.AddScoped<IOfflineSeedVerb, LxxCheckVerb>();
        services.AddScoped<UnshackledWord.Application.Features.Lxx.ILxxTextRepository, UnshackledWord.Infrastructure.Lxx.LxxTextRepository>();
        services.AddScoped<UnshackledWord.Tooling.SeedDb.Services.Swete.LxxTextImporter>();
        services.AddScoped<UnshackledWord.Tooling.SeedDb.Services.VerseSimilarity.VerseSimilarityRunner>();

        return services;
    }
}
