using Microsoft.Extensions.DependencyInjection;
using UnshackledWord.Application.Features.Backup;
using UnshackledWord.Application.Features.StrongsOccurrences;
using UnshackledWord.Application.Features.VerseMapping;
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
        services.AddScoped<IHebrewMorphemeRepository, HebrewMorphemeRepository>();
        services.AddScoped<IWordDetailService, WordDetailService>();
        services.AddScoped<IVerseMappingService, VerseMappingService>();
        services.AddScoped<IStrongsOccurrenceService, StrongsOccurrenceService>();
        return services;
    }
}
