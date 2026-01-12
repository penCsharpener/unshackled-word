using System.Text;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Abstractions.Step;
using UnshackledWord.Tooling.SeedDb.Services.Abstractions;
using UnshackledWord.Tooling.SeedDb.Services.StepBible.Models;

namespace UnshackledWord.Tooling.SeedDb.Services.StepBible;

public sealed class StepGreekMorphologyStrategy : IFileParserStrategy<List<StepGreekMorphologyEntry>>
{
    private readonly IFileService _fileService;
    private readonly IStepHebrewMorphologyRepository _repo;

    public StepGreekMorphologyStrategy(IFileService fileService, IStepHebrewMorphologyRepository repo)
    {
        _fileService = fileService;
        _repo = repo;
    }

    public async Task<List<StepGreekMorphologyEntry>> SaveToDatabase(string filePath, CancellationToken token = default)
    {
        var filter = new StepHebrewMorphologyFilter { PartOfSpeech = "Adjective" };
        var count = await _repo.CountByFilterAsync(filter, token);
        if (count > 0)
        {
            return [];
        }

        var lines = await _fileService.ReadAllLinesAsync(filePath, Encoding.UTF8, token);
        var parsedEntries = new List<StepGreekMorphologyEntry>();

        for (var index = 0; index < lines.Length; index++)
        {
            if (index == 0 || !lines[index - 1].StartsWith('$'))
            {
                continue;
            }

            var line = lines[index];
            var columns = line.Split('\t', StringSplitOptions.TrimEntries);

            if (columns.Length == 0)
            {
                continue;
            }

            var entry = columns[1]
                .Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Select(x =>
                {
                    var parts = x.Split('=', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                    return (Key: parts.Length > 0 ? parts[0] : string.Empty,
                        Value: parts.Length > 1 ? parts[1] : string.Empty);
                }).ToGreekMorphEntry();

            entry.Code = columns[0];
            parsedEntries.Add(entry);
        }

        return parsedEntries;
    }
}

public static class StepGreekMorphologyExtensions
{
    public static StepGreekMorphologyEntry ToGreekMorphEntry(this IEnumerable<(string Key, string Value)> parts)
    {
        var entry = new StepGreekMorphologyEntry();

        foreach (var (key, value) in parts)
        {
            switch (key)
            {
                case "Function":
                    entry.PartOfSpeech = value;
                    break;
                case "Tense":
                    entry.Tense = value;
                    break;
                case "Voice":
                    entry.Voice = value;
                    break;
                case "Mood":
                    entry.Mood = value;
                    break;
                case "Degree":
                    entry.Degree = value;
                    break;
                case "Case":
                    entry.Case = value;
                    break;
                case "Number":
                    entry.Number = value;
                    break;
                case "Gender":
                    entry.Gender = value;
                    break;
                case "Name type":
                    entry.NameType = value;
                    break;
                case "Person":
                    entry.Person = value;
                    break;
                case "Extra":
                    switch (value)
                    {
                        case "Comparative":
                            entry.Degree = value;
                            break;
                        default:
                            entry.Extras = value;
                            break;
                    }

                    break;
            }
        }

        return entry;
    }
}
