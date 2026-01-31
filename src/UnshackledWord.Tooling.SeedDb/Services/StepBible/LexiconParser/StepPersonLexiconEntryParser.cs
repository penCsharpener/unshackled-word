using UnshackledWord.Domain.Extensions;
using UnshackledWord.Domain.Models.BibleStructure;

namespace UnshackledWord.Tooling.SeedDb.Services.StepBible.LexiconParser;

public sealed class StepPersonLexiconEntryParser : IStepLexiconParserStrategy<ILexiconEntry<BibleEntity>>
{
    public ILexiconEntry<BibleEntity> Parse(List<(LineType LineType, string Line)> entryLines)
    {
        var entry = new PersonRecord();

        foreach (var (type, line) in entryLines)
        {
            if (type == LineType.Briefest)
            {
                entry.Briefest = line.Replace("@Briefest= ", "").Trim();
                continue;
            }

            if (type == LineType.Brief)
            {
                entry.Brief = line.Replace("@Brief= ", "").Trim();
                continue;
            }

            if (type == LineType.Short)
            {
                entry.Short = line.Replace("@Short= ", "").Trim();
                continue;
            }

            if (type == LineType.Article)
            {
                entry.Article = line.Replace("@Article= ", "").Trim();
                continue;
            }

            var parts = line.Split('\t');

            if (type == LineType.First)
            {
                ProcessFirstLine(entry, parts);

                continue;
            }

            if (type == LineType.Named)
            {
                ProcessNamed(entry, parts);
            }
        }

        return entry;
    }

    private void ProcessNamed(PersonRecord entry, string[] parts)
    {
        var hebParts = parts[2].Split('=', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        entry.OriginalSpelling = hebParts.Length > 1 ? hebParts[1] : null;
        entry.StepBibleLink = parts.Length > 4 ? parts[4] : null;
        entry.References = parts.Length > 5 ? parts[5].ParseBibleReferences().Distinct().ToArray() : null;
    }

    private void ProcessFirstLine(PersonRecord entry, string[] parts)
    {
        entry.Entity = ParsePerson(parts[0])!;
        entry.Tribe = parts.Length > 6 ? parts[6] : null;
        entry.Note = parts.Length > 7 ? parts[7].Trim('#') : null;
        entry.Gender = parts.Length > 8 ? parts[8] : null;

        if (parts[2].IsNotNullOrWhiteSpace() && parts[2] != " + ")
        {
            entry.Parents = parts[2].Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(x => ParsePerson(x)).ToArray();
        }

        if (parts[3].IsNotNullOrWhiteSpace())
        {
            entry.Siblings = parts[3].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(x => ParsePerson(x)).ToArray();
        }

        if (parts[4].IsNotNullOrWhiteSpace())
        {
            entry.Partners = parts[4].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(x => ParsePerson(x)).ToArray();
        }

        if (parts[5].IsNotNullOrWhiteSpace())
        {
            entry.Offspring = parts[5].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(x => ParsePerson(x)).ToArray();
        }
    }

    private BibleEntity ParsePerson(string personText)
    {
        var NameAndRefSplit = personText.Split(['@', '='], StringSplitOptions.TrimEntries);
        var name = NameAndRefSplit[0];
        var strongs = NameAndRefSplit.Length > 2 ? NameAndRefSplit[2] : default(string);

        var firstOccParts = NameAndRefSplit[1].Split(['.', '-'], StringSplitOptions.TrimEntries);
        var firstOccBook = BibleBook.FindByAbbreviation(firstOccParts[0])!.Value;
        int.TryParse(firstOccParts[1], out var firstOccChapter);
        int.TryParse(firstOccParts[2], out var firstOccVerse);
        var firstOccRef = new BibleReference(firstOccBook.Id, firstOccChapter, firstOccVerse);

        return new BibleEntity() { Name = name, FirstOccurance = firstOccRef, Strongs = strongs };
    }
}
