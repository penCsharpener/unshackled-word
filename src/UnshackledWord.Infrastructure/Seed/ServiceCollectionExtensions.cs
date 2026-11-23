using Microsoft.Extensions.DependencyInjection;
using UnshackledWord.Infrastructure.Seed.BaseText;
using UnshackledWord.Infrastructure.Seed.Mappings;
using UnshackledWord.Infrastructure.Seed.Morphology;
using UnshackledWord.Infrastructure.Seed.Revision;
using UnshackledWord.Infrastructure.Seed.Tsk;
using UnshackledWord.Infrastructure.Seed.Vocab;
using UnshackledWord.Infrastructure.Vocab;

namespace UnshackledWord.Infrastructure.Seed;

public static class ServiceCollectionExtensions
{
    /// <summary>The seed exporters (read-only) and the manifest store; used by the SeedDb CLI and by the Web API's maintainer download.</summary>
    public static IServiceCollection AddSeedExportServices(this IServiceCollection services)
    {
        services.AddScoped<SeedManifestStore>();
        services.AddScoped<MorphologyExportRepository>();
        services.AddScoped<MorphologyExporter>();
        services.AddScoped<MappingExportRepository>();
        services.AddScoped<MappingExporter>();
        services.AddScoped<RevisionExportRepository>();
        services.AddScoped<RevisionExporter>();
        services.AddScoped<VocabSeedExporter>();
        services.AddScoped<SeedExportRunner>();
        services.AddScoped<SeedFileExportService>();
        services.AddScoped<TskCurationSeedService>();

        return services;
    }

    /// <summary>
    /// The seed importers, the rebase and the exporters: everything the SeedDb CLI and the maintainer page run. The caller registers
    /// <c>IDbReader</c>, <c>IDbWriter</c>, <c>IDbConnectionFactory</c>, <c>IFileService</c> and <c>AppSettings</c>.
    /// </summary>
    public static IServiceCollection AddSeedImportServices(this IServiceCollection services)
    {
        services.AddSeedExportServices();
        services.AddScoped<ElbWordTokenReader>();
        services.AddScoped<Elberfelder1871Strategy>();
        services.AddScoped<Elberfelder1871VerseStrategy>();
        services.AddScoped<MorphologySeedRepository>();
        services.AddScoped<MorphologySeedImporter>();
        services.AddScoped<MappingSeedRepository>();
        services.AddScoped<MappingSeedSourceReader>();
        services.AddScoped<MappingSeedImporter>();
        services.AddScoped<RevisionSeedRepository>();
        services.AddScoped<RevisionSeedSourceReader>();
        services.AddScoped<RevisionSeedImporter>();
        services.AddScoped<RevisionBaseRealigner>();
        services.AddScoped<VocabSeedImporter>();
        services.AddScoped<RebaseRepository>();
        services.AddScoped<SeedRebaseService>();
        services.AddScoped<SeedImportsRepository>();
        services.AddScoped<VocabLexiconGenerator>();
        services.AddScoped<VocabRenderingsRefresher>();
        services.AddScoped<VocabMeaningDeriver>();

        return services;
    }
}
