using System.Globalization;
using CsvHelper;
using Microsoft.Extensions.DependencyInjection;

namespace UnshackledWord.Tooling.AiWorker;

public class ElberfelderMapping
{
    private readonly ITestOutputHelper _output;
    private readonly IServiceProvider _serviceProvider;


    public ElberfelderMapping(ITestOutputHelper output)
    {
        _output = output;
        _serviceProvider = Program.SetupDependencies(null, null);
    }

    [Fact]
    public async Task Test1()
    {
        var repo = _serviceProvider.GetRequiredService<MappingRepository>();
        var structureData = await repo.GetGreekNtStructureByChapterAsync();

        var client = _serviceProvider.GetRequiredService<GeminiFlashClient>();

        foreach (var bRef in structureData)
        {
            foreach (var verseChunk in Enumerable.Range(1, bRef.Verse).Chunk(10))
            {
                var minVerse = verseChunk.Min();
                var maxVerse = verseChunk.Max();

                var elbWords = await repo.GetElbVerseDataAsync(bRef.BibleBookId, bRef.Chapter, minVerse, maxVerse);
                var stepWords = await repo.GetStepGreekVerseDataAsync(bRef.BibleBookId, bRef.Chapter, minVerse, maxVerse);

                var response = await client.GetElbStepMappings(elbWords, stepWords, TestContext.Current.CancellationToken);

                await using var writer = new StringWriter();
                await using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
                await csv.WriteRecordsAsync(response, TestContext.Current.CancellationToken);

                _output.WriteLine(writer.ToString());
            }
        }
    }
}
