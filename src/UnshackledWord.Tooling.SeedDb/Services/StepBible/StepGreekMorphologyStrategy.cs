using System.Text;
using System.Text.RegularExpressions;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Tooling.SeedDb.Services.Abstractions;
using UnshackledWord.Tooling.SeedDb.Services.StepBible.Models;

namespace UnshackledWord.Tooling.SeedDb.Services.StepBible;

public sealed partial class StepGreekMorphologyStrategy : IFileParserStrategy<List<StepGreekMorphologyEntry>>
{
    private readonly IFileService _fileService;
    private static Regex _lineStart = IsLineStart();

    public StepGreekMorphologyStrategy(IFileService fileService)
    {
        _fileService = fileService;
    }

    public async Task<List<StepGreekMorphologyEntry>> SaveToDatabase(string filePath, CancellationToken token = default)
    {
        var lines = await _fileService.ReadAllLinesAsync(filePath, Encoding.UTF8, token);

        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            var columns = line.Split('\t', StringSplitOptions.TrimEntries);
            var isDataLine = false;

            if (columns.Length == 0)
            {
                continue;
            }

            if (_lineStart.IsMatch(line) && !isDataLine)
            {
                isDataLine = true;
            }

            if (isDataLine)
            {

            }
        }

        return [];
    }

    [GeneratedRegex("^G\\d{4,5}\t")]
    private static partial Regex IsLineStart();
}
