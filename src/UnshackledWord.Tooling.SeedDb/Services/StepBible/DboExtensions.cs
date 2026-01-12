using UnshackledWord.Domain.Models.Dbo.Step;
using UnshackledWord.Tooling.SeedDb.Services.StepBible.Models;

namespace UnshackledWord.Tooling.SeedDb.Services.StepBible;

public static class DboExtensions
{
    public static IEnumerable<StepGreekWordDbo> ToDbo(this IEnumerable<StepAmalgamatedGreekEntry> entries)
    {
        foreach (var entry in entries)
        {
            yield return new StepGreekWordDbo
            {
                BibleBookId = entry.BibleBook.Id,
                Chapter = entry.Chapter,
                Verse = entry.Verse,
                PositionInVerse = entry.PositionInVerse,
                AltChapter = entry.AlternativeChapter,
                AltVerse = entry.AlternativeVerse,
                Lemma = entry.Lemma,
                LemmaNoDiacritics = entry.LemmaNoDiacritics,
                Morphology = entry.Morphology,
                Transliteration = entry.Transliteration,
                IsInNestleAland = entry.FoundInNestleAland,
                IsInTextusReceptus = entry.FoundInTextusReceptus,
                IsInOther = entry.FoundInOther,
                Type = entry.Type,
                Greek = entry.Greek,
                GreekNoDiacritics = entry.GreekNoDiacritics,
                English = entry.EnglishTranslation,
                DisambiguatedStrongs = entry.DisambiguatedStrongs,
                Gloss = entry.Gloss,
                Editions = entry.Editions,
                Spanish = entry.SpanishTranslation,
                MeaningVariants = entry.MeaningVariants,
                SpellingVariants = entry.SpellingVariants,
                SubMeaning = entry.SubMeaning,
                ConjoinWord = entry.ConjoinWord,
                StrongInstance = entry.StrongInstance,
                AltStrongs = entry.AltStrongs
            };
        }
    }

    public static IEnumerable<StepHebrewWordDbo> ToDbo(this IEnumerable<StepAmalgamatedHebrewEntry> entries)
    {
        foreach (var entry in entries)
        {
            yield return new StepHebrewWordDbo
            {
                BibleBookId = entry.BibleBook.Id,
                Chapter = entry.Chapter,
                Verse = entry.Verse,
                PositionInVerse = entry.PositionInVerse,
                AltChapter = entry.AlternativeChapter,
                AltVerse = entry.AlternativeVerse,
                Type = entry.Type,
                HebrewNormalised = entry.HebrewNormalised,
                Hebrew = entry.Hebrew,
                HebrewNoDiacritics = entry.HebrewNoDiacritics,
                Transliteration = entry.Transliteration,
                Gloss = entry.Gloss,
                DisambiguatedStrongs = entry.DisambiguatedStrongs,
                Grammar = entry.Grammar,
                MeaningVariants = entry.MeaningVariants,
                SpellingVariants = entry.SpellingVariants,
                RootDisambiguatedStrongsInstance = entry.RootDisambiguatedStrongsInstance,
                AlternativeStrongs = entry.AlternativeStrongs,
                ConjoinWord = entry.ConjoinWord,
                ExpandedStrongTags = entry.ExpandedStrongTags
            };
        }
    }

    public static IEnumerable<StepStrongsDbo> ToDbo(this IEnumerable<StepGreekStrongsEntry> entries)
    {
        foreach (var entry in entries)
        {
            yield return new StepStrongsDbo
            {
                ExtendedStrongs = entry.ExtendedStrongs,
                DisambiguatedStrongs = entry.DisambiguatedStrongs,
                UnifiedStrongs = entry.UnifiedStrongs,
                OriginalWord = entry.OriginalWord,
                OriginalWordNoDiacritics = entry.OriginalWordNoDiacritics,
                Transliteration = entry.Transliteration,
                Morphology = entry.Morphology,
                Gloss = entry.Gloss,
                Lexicon = entry.AbbottSmithLexicon,
            };
        }
    }

    public static IEnumerable<StepStrongsDbo> ToDbo(this IEnumerable<StepHebrewStrongsEntry> entries)
    {
        foreach (var entry in entries)
        {
            yield return new StepStrongsDbo
            {
                ExtendedStrongs = entry.ExtendedStrongs,
                DisambiguatedStrongs = entry.DisambiguatedStrongs,
                UnifiedStrongs = entry.UnifiedStrongs,
                OriginalWord = entry.OriginalWord,
                OriginalWordNoDiacritics = entry.OriginalWordNoDiacritics,
                Transliteration = entry.Transliteration,
                Morphology = entry.Morphology,
                Gloss = entry.Gloss,
                Lexicon = entry.Meaning,
            };
        }
    }

    public static IEnumerable<StepHebrewMorphologyDbo> ToDbo(this IEnumerable<StepHebrewMorphologyEntry> entries)
    {
        foreach (var entry in entries)
        {
            yield return new StepHebrewMorphologyDbo
            {
                Code = entry.Code,
                PartOfSpeech = entry.PartOfSpeech,
                Form = entry.Form,
                Tense = entry.Tense,
                Mood = entry.Mood,
                Person = entry.Person,
                Number = entry.Number,
                Gender = entry.Gender,
                State = entry.State,
            };
        }
    }
}
