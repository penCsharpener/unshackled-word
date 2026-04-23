using System.Text;
using UnshackledWord.Domain.Models.BibleStructure;
using UnshackledWord.Tooling.AiWorker.Mapping.Models;

namespace UnshackledWord.Tooling.AiWorker.Mapping;

public static class MappingPromptExtensions
{
    public static string ToDelimitedString(this IEnumerable<VerseDataList<VerseDataWithoutOrder>> verseDataItems)
    {
        var sb = new StringBuilder();
        foreach (var item in verseDataItems)
        {
            var reference = $"[Ref {item.BookId}:{item.Chapter}:{item.Verse}[";
            sb.Append(reference);
            foreach (var word in item.Data)
            {
                sb.Append($"({word.Id},{word.Word})");
            }

            sb.Append("]]");
        }

        return sb.ToString();
    }

    public static IEnumerable<VerseDataList<ElbStepAiMapping>> ToTypedResponse(this List<VerseDataList<string>> verses)
    {
        foreach (var verse in verses)
        {
            var result = new VerseDataList<ElbStepAiMapping>()
            {
                BookId = verse.BookId,
                Chapter = verse.Chapter,
                Verse = verse.Verse,
                RefId = verse.RefId,
                Data = []
            };

            if (result.BookId == 0 && result.RefId > 0)
            {
                var bRef = BibleReference.FromRefId(result.RefId);
                result.BookId = bRef.BookId;
                result.Chapter = bRef.Chapter;
                result.Verse = bRef.Verse;
            }

            var allData = new List<ElbStepAiMapping>();

            foreach (var item in verse.Data)
            {
                var mapping = new ElbStepAiMapping();

                var parts = item.Split("|", StringSplitOptions.TrimEntries | StringSplitOptions.TrimEntries);
                mapping.ElbWordId = int.Parse(parts[0]);
                mapping.StepWordId = ParseNullableInt(parts[1]);
                mapping.IsAddedWord = ParseBoolean(parts[2]);
                mapping.ParentElbWordId = ParseNullableInt(parts[3]);
                mapping.PartOrder = ParseNullableInt(parts[4]);
                mapping.GermanWordPart = parts.Length > 5 ? parts[5] == "-" ? null : parts[5] : null;

                allData.Add(mapping);
            }

            result.Data = allData.ToArray();

            yield return result;
        }
    }

    private static bool ParseBoolean(string value)
    {
        var boolInt = int.Parse(value);

        return boolInt == 1;
    }

    private static int? ParseNullableInt(string value)
    {
        if (int.TryParse(value, out int result))
        {
            return result;
        }

        return null;
    }
}
