using UnshackledWord.Tooling.SeedDb.Services.Abstractions;

namespace UnshackledWord.Tooling.SeedDb.Services.Elb1871WordsSrGntWordsMapper;

public class Elb1871SrGntStrategy : IFileParserStrategy
{
    private readonly Elb1871SrGntRepository _repository;
    private readonly MappingFileReader _mappingFileReader;

    public Elb1871SrGntStrategy(Elb1871SrGntRepository repository, MappingFileReader mappingFileReader)
    {
        _repository = repository;
        _mappingFileReader = mappingFileReader;
    }

    public async Task SaveToDatabase(string filePath, CancellationToken token = default)
    {
        var mappings = await _mappingFileReader.GetMappingsAsync(token);
        var elbVerses = await _repository.GetElb1871Async(token);
        var srVerses = await _repository.GetSrWordsAsync(token);
        var dboList = new List<Elb1871SrGntTaggingDbo>();
        var matchCounter = 0;

        foreach (var map in mappings)
        {
            foreach (var elbVerse in elbVerses)
            {
                var srVerse = srVerses.FirstOrDefault(x => x.BibleBookId == elbVerse.BibleBookId &&
                                                           x.ChapterId == elbVerse.ChapterId &&
                                                           x.VerseId == elbVerse.VerseId);

                if (srVerse is null)
                {
                    continue;
                }

                foreach (var srW in srVerse.Words)
                {
                    if (srW.Word != map.SrGntWord)
                    {
                        continue;
                    }

                    foreach (var eW in elbVerse.Words)
                    {
                        foreach (var mapElbWord in map.Elb1871WordList)
                        {
                            if (eW.Word == mapElbWord)
                            {
                                eW.Strongs = srW.Strongs;
                                matchCounter++;
                            }
                        }
                    }
                }
            }
        }
    }
}
