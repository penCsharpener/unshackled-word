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
        var structureData = await repo.GetGreekNtStructureAsync();

        var client = _serviceProvider.GetRequiredService<GeminiFlashClient>();

        foreach (var bRef in structureData)
        {
            var elbWords = await repo.GetElbVerseDataAsync(bRef.BibleBookId, bRef.Chapter, bRef.Verse);
            var stepWords = await repo.GetStepGreekVerseDataAsync(bRef.BibleBookId, bRef.Chapter, bRef.Verse);

            var response = await client.GetElbStepMappings(elbWords, stepWords, TestContext.Current.CancellationToken);

            await using var writer = new StringWriter();
            await using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
            await csv.WriteRecordsAsync(response);

            _output.WriteLine(writer.ToString());
        }
    }
}
