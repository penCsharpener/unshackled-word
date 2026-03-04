using UnshackledWord.Tooling.AiWorker.Models;

namespace UnshackledWord.Tooling.AiWorker;

public class Worker : BackgroundService
{
    private readonly GreekMappingService _gkMapping;
    private readonly HebrewMappingService _hebMapping;

    public Worker(GreekMappingService gkMapping, HebrewMappingService hebMapping)
    {
        _gkMapping = gkMapping;
        _hebMapping = hebMapping;
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
