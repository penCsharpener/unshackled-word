using UnshackledWord.Domain.Models.BibleStructure;
using UnshackledWord.Domain.Models.Dbo.Step;

namespace UnshackledWord.Tooling.SeedDb.Services.StepBible.LexiconParser;

public static class ParserExtensions
{
    public static IEnumerable<BibleReference> ParseBibleReferences(this string refText)
    {
        var references = refText.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        foreach (var reference in references)
        {
            var parts = reference.Split('.', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            var book = BibleBook.FindByAbbreviation(parts[0]);

            if (book is null)
            {
                continue;
            }

            int.TryParse(parts[1], out var firstOccChapter);
            int.TryParse(string.Concat(parts[2].Where(char.IsDigit)), out var firstOccVerse);

            yield return new BibleReference(book.Value.Id, firstOccChapter, firstOccVerse);
        }
    }

    public static IEnumerable<StepPersonLexiconDbo> ToDbo(this IEnumerable<PersonRecord> persons)
    {
        foreach (var person in persons)
        {
            var dbo = new StepPersonLexiconDbo
            {
                Article = person.Article,
                LxxRefId = new BibleReference(person.Entity.FirstOccurance.BookId, person.Entity.FirstOccurance.Chapter, person.Entity.FirstOccurance.Verse).RefId,
                Gender = person.Gender,
                Briefest = person.Briefest,
                Short = person.Short,
                Brief = person.Brief,
                Name = person.Entity.Name,
                Strongs = person.Entity.Strongs,
                Note = person.Note,
                OriginalSpelling = person.OriginalSpelling,
                Tribe = person.Tribe
            };

            var parents = person.Parents.ToDbo("Parent", dbo.Id);
            var siblings = person.Siblings.ToDbo("Sibling", dbo.Id);
            var partners = person.Partners.ToDbo("Partner", dbo.Id);
            var children = person.Offspring.ToDbo("Child", dbo.Id);

            dbo.Relations = [];
            dbo.Relations.AddRange(parents);
            dbo.Relations.AddRange(siblings);
            dbo.Relations.AddRange(partners);
            dbo.Relations.AddRange(children);

            yield return dbo;
        }
    }

    public static IEnumerable<StepPersonLexiconRelationsDbo> ToDbo(this IEnumerable<BibleEntity>? persons, string type, int parentId = 0)
    {
        if (persons is null)
        {
            yield break;
        }

        foreach (var person in persons)
        {
            yield return new StepPersonLexiconRelationsDbo
            {
                Name = person.Name,
                LxxRefId = new BibleReference(person.FirstOccurance.BookId, person.FirstOccurance.Chapter, person.FirstOccurance.Verse).RefId,
                Strongs = person.Strongs,
                PersonLexiconId = parentId,
                RelationType = type
            };
        }
    }

    public static IEnumerable<StepPlaceLexiconDbo> ToDbo(this IEnumerable<PlaceRecord> places)
    {
        foreach (var place in places)
        {
            yield return new StepPlaceLexiconDbo
            {
                Article = place.Article,
                LxxRefId = new BibleReference(place.Entity.FirstOccurance.BookId, place.Entity.FirstOccurance.Chapter, place.Entity.FirstOccurance.Verse).RefId,
                Briefest = place.Briefest,
                Short = place.Short,
                Brief = place.Brief,
                Name = place.Entity.Name,
                Strongs = place.Entity.Strongs,
                Note = place.Note,
                OriginalSpelling = place.OriginalSpelling,
                GoogleMapsLinks = place.GoogleMapsLinks,
                PalOpenMapsLink = place.PalOpenMapsLink,
                StepBibleLink = place.StepBibleLink,
                Type = place.Type
            };
        }
    }

    public static IEnumerable<StepOtherLexiconDbo> ToDbo(this IEnumerable<OtherRecord> places)
    {
        foreach (var place in places)
        {
            yield return new StepOtherLexiconDbo
            {
                Article = place.Article,
                LxxRefId = new BibleReference(place.Entity.FirstOccurance.BookId, place.Entity.FirstOccurance.Chapter, place.Entity.FirstOccurance.Verse).RefId,
                Briefest = place.Briefest,
                Short = place.Short,
                Brief = place.Brief,
                Name = place.Entity.Name,
                Strongs = place.Entity.Strongs,
                Note = place.Note,
                OriginalSpelling = place.OriginalSpelling,
                StepBibleLink = place.StepBibleLink,
                Type = place.Type,
            };
        }
    }
}
