using UnshackledWord.Application.Abstractions;
using UnshackledWord.Tooling.SeedDb.Services.Abstractions;

namespace UnshackledWord.Tooling.SeedDb.Services;

public sealed class SrTsvParserStrategy : IFileParserStrategy
{
    private readonly IFileService _fileService;
    private readonly ParseHelper _parseHelper;

    public SrTsvParserStrategy(IFileService fileService, ParseHelper parseHelper)
    {
        _fileService = fileService;
        _parseHelper = parseHelper;
    }

    public async Task SaveToDatabase(string filePath, CancellationToken token = default)
    {
        var lines = await _fileService.ReadAllLinesAsync(filePath, token);
        var wordList = new List<WordInfo>();

        foreach (var line in lines.Skip(1))
        {
            var parts = line.Split('\t');
            if (parts.Length < 7)
            {
                continue;
            }

            var bibleBook = _parseHelper.ParseNtVerseId(parts[0]);
            var wordInContext = parts[1];
            var koineWord = parts[2];
            var lemma = parts[3];
            var strongs = parts[4];
            var partOfSpeech = parts[5];
            var conjugation = parts[6];

            var wordInfo = new WordInfo(
                bibleBook,
                wordInContext,
                koineWord,
                lemma,
                strongs,
                partOfSpeech,
                conjugation
            );

            wordList.Add(wordInfo);
        }
    }
}
