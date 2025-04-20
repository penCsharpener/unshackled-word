using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Models.BibleStructure;
using UnshackledWord.Tooling.SeedDb.Models;
using UnshackledWord.Tooling.SeedDb.Services.Abstractions;

namespace UnshackledWord.Tooling.SeedDb.Services;

public sealed class SrTxtParserStrategy : IFileParserStrategy
{
    private readonly IFileService _fileService;

    public SrTxtParserStrategy(IFileService fileService)
    {
        _fileService = fileService;
    }

    public async Task SaveToDatabase(string filePath, CancellationToken token = default)
    {
        var lines = await _fileService.ReadAllLinesAsync(filePath, token);
        var verseList = new List<SrVerse>();

        foreach (var line in lines)
        {
            var verseString = line[0..8];
            var bookId = int.Parse(verseString[0..2]);
            var chapterId = int.Parse(verseString[2..5]);
            var verseId = int.Parse(verseString[5..8]);
            var ntId = int.Parse(verseString);
            var verseText = line[9..];

            verseList.Add(new SrVerse(ntId, BibleBook.NewTestamentBooks[bookId], chapterId, verseId, verseText));
        }

        var groupedByBook = verseList.GroupBy(v => v.BibleBook).ToDictionary(key => key.Key, value => value.ToList());
    }
}
