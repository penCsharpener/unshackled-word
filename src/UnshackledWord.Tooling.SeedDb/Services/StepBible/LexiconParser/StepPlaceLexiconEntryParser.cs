using UnshackledWord.Domain.Models.BibleStructure;

namespace UnshackledWord.Tooling.SeedDb.Services.StepBible.LexiconParser;

public sealed class StepPlaceLexiconEntryParser : IStepLexiconParserStrategy<ILexiconEntry<BibleEntity>>
{
    public ILexiconEntry<BibleEntity> Parse(List<(LineType LineType, string Line)> entryLines)
    {
        var entry = new PlaceRecord();

        foreach (var (type, line) in entryLines)
        {
            if (type == LineType.Briefest)
            {
                entry.Briefest = line.Replace("@Briefest=", "").Trim();
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

                continue;
            }
        }

        return entry;
    }

    private void ProcessFirstLine(PlaceRecord entry, string[] parts)
    {
        entry.Entity = ParsePlace(parts[0])!;
        // entry.Tribe = parts.Length > 6 ? parts[6] : null;
        entry.Note = parts.Length > 7 ? parts[7].Trim('#') : null;
        entry.Type = parts.Length > 8 ? parts[8] : null;
        entry.GoogleMapsLinks = parts.Length > 4 ? parts[4] : null;
        entry.PalOpenMapsLink = parts.Length > 5 ? parts[5] : null;
    }

    private void ProcessNamed(PlaceRecord entry, string[] parts)
    {
        var hebParts = parts[2].Split('=', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        entry.OriginalSpelling = hebParts.Length > 1 ? hebParts[1] : null;
        entry.StepBibleLink = parts.Length > 4 ? parts[4] : null;
        entry.References = parts.Length > 5 ? parts[5].ParseBibleReferences().Distinct().ToArray() : null;
    }

    private BibleEntity ParsePlace(string placeText)
    {
        var NameAndRefSplit = placeText.Split(['@', '='], StringSplitOptions.TrimEntries);
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
