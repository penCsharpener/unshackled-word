using System.Text;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Extensions;
using UnshackledWord.Tooling.SeedDb.Services.Abstractions;
using UnshackledWord.Tooling.SeedDb.Services.StepBible.Models;

namespace UnshackledWord.Tooling.SeedDb.Services.StepBible;

public sealed class StepHebrewMorphologyStrategy : IFileParserStrategy<List<StepHebrewMorphologyEntry>>
{
    private readonly IFileService _fileService;

    public StepHebrewMorphologyStrategy(IFileService fileService)
    {
        _fileService = fileService;
    }

    public async Task<List<StepHebrewMorphologyEntry>> SaveToDatabase(string filePath, CancellationToken token = default)
    {
        var lines = await _fileService.ReadAllLinesAsync(filePath, Encoding.UTF8, token);
        var parsedEntries = new List<StepHebrewMorphologyEntry>();

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
                .SplitIgnoringParentheses(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Select(x =>
                {
                    var parts = x.SplitIgnoringParentheses('=', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToArray();
                    return (Key: parts.Length > 0 ? parts[0] : string.Empty,
                        Value: parts.Length > 1 ? parts[1] : string.Empty);
                }).ToHebrewMorphEntry();

            entry.Code = columns[0];
            parsedEntries.Add(entry);
        }

        return parsedEntries;
    }
}

public static class StepHebrewMorphologyExtensions
{
    public static StepHebrewMorphologyEntry ToHebrewMorphEntry(this IEnumerable<(string Key, string Value)> parts)
    {
        var entry = new StepHebrewMorphologyEntry();

        foreach (var (key, value) in parts)
        {
            switch (key)
            {
                case "Function":
                    entry.PartOfSpeech = value;
                    break;
                case "Form":
                    if (value.Contains("hence Tense"))
                    {
                        var splitByParen = value.Split(['(', ')'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        entry.Form = splitByParen[0];
                        var actionAndVoice = splitByParen[1].Replace("hence Tense", "Tense").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        foreach (var av in actionAndVoice)
                        {
                            var actionVoiceSplit = av.Split('=',
                                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                            if (actionVoiceSplit.Length == 2)
                            {
                                var avKey = actionVoiceSplit[0];
                                var avValue = actionVoiceSplit[1];
                                if (avKey == "Tense")
                                {
                                    entry.Tense = avValue;
                                }
                                else if (avKey == "Mood")
                                {
                                    entry.Mood = avValue;
                                }
                            }
                        }
                    }
                    else
                    {
                        entry.Form = value;
                    }
                    break;
                case "Stem":
                    if (value.Contains("hence Action"))
                    {
                        var splitByParen = value.Split(['(', ')'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        entry.Stem = splitByParen[0];
                        var actionAndVoice = splitByParen[1].Replace("hence Action", "Action").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        foreach (var av in actionAndVoice)
                        {
                            var actionVoiceSplit = av.Split('=',
                                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                            if (actionVoiceSplit.Length == 2)
                            {
                                var avKey = actionVoiceSplit[0];
                                var avValue = actionVoiceSplit[1];
                                if (avKey == "Action")
                                {
                                    entry.Action = avValue;
                                }
                                else if (avKey == "Voice")
                                {
                                    entry.Voice = avValue;
                                }
                            }
                        }
                    }
                    else
                    {
                        entry.Stem = value;
                    }
                    break;
                case "Number":
                    entry.Number = value;
                    break;
                case "Gender":
                    entry.Gender = value;
                    break;
                case "State":
                    entry.State = value;
                    break;
                case "Person":
                    entry.Person = value;
                    break;
            }
        }

        return entry;
    }
}
