using UnshackledWord.Domain.Models.BibleStructure;
using UnshackledWord.Domain.Models.Dbo.Step;
using UnshackledWord.Tooling.SeedDb.Services.StepBible.Models;
using UnshackledWord.Tooling.SeedDb.Services.StepBible.StrongsToText;

namespace UnshackledWord.Tooling.SeedDb.Services.StepBible;

public static class DboExtensions
{
    public static IEnumerable<StepGreekWordDbo> ToDbo(this IEnumerable<StepAmalgamatedGreekEntry> entries)
    {
        foreach (var entry in entries)
        {
            yield return new StepGreekWordDbo
            {
                LxxRefId = new BibleReference(entry.BibleBook.Id, entry.Chapter, entry.Verse).RefId,
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
                AltStrongs = entry.AltStrongs,
            };
        }
    }

    public static IEnumerable<StepHebrewWordDbo> ToDbo(this IEnumerable<StepAmalgamatedHebrewEntry> entries)
    {
        foreach (var entry in entries)
        {
            yield return new StepHebrewWordDbo
            {
                LxxRefId = new BibleReference(entry.BibleBook.Id, entry.Chapter, entry.Verse).RefId,
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
                ExpandedStrongTags = entry.ExpandedStrongTags,
            };
        }
    }

    public static IEnumerable<StepStrongsLexiconDbo> ToDbo(this IEnumerable<StepGreekStrongsEntry> entries)
    {
        foreach (var entry in entries)
        {
            yield return new StepStrongsLexiconDbo
            {
                LanguageId = entry.LanguageId,
                Number = entry.Number,
                DisambiguatedExtra = entry.DisambiguatedExtra,
                Extra = entry.Extra,
                UnifiedStrongs = entry.UnifiedEntries.ToDbo().ToList(),
                OriginalWord = entry.OriginalWord,
                OriginalWordNoDiacritics = entry.OriginalWordNoDiacritics,
                Transliteration = entry.Transliteration,
                Morphology = entry.Morphology,
                Gloss = entry.Gloss,
                Lexicon = entry.AbbottSmithLexicon,
            };
        }
    }

    public static IEnumerable<StepStrongsLexiconDbo> ToDbo(this IEnumerable<StepHebrewStrongsEntry> entries)
    {
        foreach (var entry in entries)
        {
            yield return new StepStrongsLexiconDbo
            {
                LanguageId = entry.LanguageId,
                Number = entry.Number,
                DisambiguatedExtra = entry.DisambiguatedExtra,
                Extra = entry.Extra,
                UnifiedStrongs = entry.UnifiedEntries.ToDbo().ToList(),
                OriginalWord = entry.OriginalWord,
                OriginalWordNoDiacritics = entry.OriginalWordNoDiacritics,
                Transliteration = entry.Transliteration,
                Morphology = entry.Morphology,
                Gloss = entry.Gloss,
                Lexicon = entry.Meaning,
            };
        }
    }

    public static IEnumerable<StepStrongsToTextDbo> ToDbo(this IEnumerable<StrongsNumberInternal> entries, int? hebrewWordId, int? greekWordId)
    {
        foreach (var entry in entries)
        {
            yield return new StepStrongsToTextDbo
            {
                LanguageId = entry.LanguageId,
                Number = entry.Number,
                Extra = entry.Extra,
                IsRoot = entry.IsRoot,
                CoversNextWord = entry.CoversNextWord,
                StepGreekWordId = greekWordId,
                StepHebrewWordId = hebrewWordId,
                Order = entry.Order
            };
        }
    }

    public static IEnumerable<StepUnifiedStrongsDbo> ToDbo(this IEnumerable<StepStrongsUnifiedEntry> entries)
    {
        foreach (var entry in entries)
        {
            yield return new StepUnifiedStrongsDbo
            {
                LanguageId = entry.LanguageId, Extra = entry.Extra, Number = entry.Number,
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

    public static IEnumerable<StepGreekMorphologyDbo> ToDbo(this IEnumerable<StepGreekMorphologyEntry> entries)
    {
        foreach (var entry in entries)
        {
            yield return new StepGreekMorphologyDbo
            {
                Code = entry.Code,
                PartOfSpeech = entry.PartOfSpeech,
                Voice = entry.Voice,
                Tense = entry.Tense,
                Mood = entry.Mood,
                Person = entry.Person,
                Number = entry.Number,
                Gender = entry.Gender,
                Degree = entry.Degree,
                Extras = entry.Extras,
                NameType = entry.NameType
            };
        }
    }
}
