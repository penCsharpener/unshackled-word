using System.Text.Json;
using UnshackledWord.Tooling.AiWorker.Models;
using UnshackledWord.Tooling.AiWorker.Models.Greek;

namespace UnshackledWord.Tooling.AiWorker;

public class Worker : BackgroundService
{
    private readonly GreekMappingService _gkMapping;
    private readonly HebrewMappingService _hebMapping;
    private readonly GreekMappingRepository _greekMappingRepository;

    public Worker(GreekMappingService gkMapping, HebrewMappingService hebMapping, GreekMappingRepository greekMappingRepository)
    {
        _gkMapping = gkMapping;
        _hebMapping = hebMapping;
        _greekMappingRepository = greekMappingRepository;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // var minVerse = 26;
        // var bRef = new BibleReferenceRange
        // {
        //     BibleBookId = 42,
        //     Chapter = 16,
        //     MinVerse = minVerse,
        //     MaxVerse = 30
        // };
        // await _gkMapping.MapWordsForRangeAsync(minVerse, 30, bRef, stoppingToken);
        await _gkMapping.RunAsync(stoppingToken);
        //await _hebMapping.RunAsync(stoppingToken);
    }
}
