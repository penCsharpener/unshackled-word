using UnshackledWord.Tooling.AiWorker.Mapping.Models.Greek;
using UnshackledWord.Tooling.AiWorker.Mapping.Models.Hebrew;

namespace UnshackledWord.Tooling.AiWorker.Mapping.Models;

public static class VerseDataExtensions
{
    public static IEnumerable<VerseDataList<VerseDataWithoutOrder>> ReduceIds(this IEnumerable<VerseDataList<VerseDataWithoutOrder>> items, IntShrinkDictionary tempMapping)
    {
        foreach (var item in items)
        {
            foreach (var verse in item.Data)
            {
                var newId = tempMapping.AddIds(verse.Id);
                verse.Id = newId;
            }

            yield return item;
        }
    }

    public static IEnumerable<VerseDataList<ElbStepAiMapping>> RestoreIds(
        this IEnumerable<VerseDataList<ElbStepAiMapping>> items, IntShrinkDictionary tempMapping, Action<IntShrinkDictionary, ElbStepAiMapping> action)
    {
        foreach (var item in items)
        {
            foreach (var verse in item.Data)
            {
                action(tempMapping, verse);
            }

            yield return item;
        }
    }

    public static IEnumerable<VerseDataList<VerseDataWithoutOrder>> ToWithoutOrder(
        this IEnumerable<VerseDataList<ElbVerseData>> items)
    {
        return items.Select(x => new VerseDataList<VerseDataWithoutOrder>
        {
            BookId = x.BookId,
            Chapter = x.Chapter,
            Verse = x.Verse,
            Data = x.Data.Select(k => new VerseDataWithoutOrder { Id = k.Id, Word = k.German }).ToList()
        });
    }

    public static IEnumerable<VerseDataList<VerseDataWithoutOrder>> ToWithoutOrder(
        this IEnumerable<VerseDataList<StepGreekVerseData>> items)
    {
        return items.Select(x => new VerseDataList<VerseDataWithoutOrder>
        {
            BookId = x.BookId,
            Chapter = x.Chapter,
            Verse = x.Verse,
            Data = x.Data.Select(k => new VerseDataWithoutOrder { Id = k.Id, Word = k.Greek }).ToList()
        });
    }

    public static IEnumerable<VerseDataList<VerseDataWithoutOrder>> ToWithoutOrder(
        this IEnumerable<VerseDataList<StepHebrewVerseData>> items)
    {
        return items.Select(x => new VerseDataList<VerseDataWithoutOrder>
        {
            BookId = x.BookId,
            Chapter = x.Chapter,
            Verse = x.Verse,
            Data = x.Data.Select(k => new VerseDataWithoutOrder { Id = k.Id, Word = k.Hebrew }).ToList()
        });
    }
}
