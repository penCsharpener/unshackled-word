using UnshackledWord.Domain.Models.BibleStructure;

namespace UnshackledWord.Domain.Tests.Model.BibleStructure;

public class BibleReferenceValidationTests
{
    private readonly ITestOutputHelper _outputHelper;

    public BibleReferenceValidationTests(ITestOutputHelper outputHelper)
    {
        _outputHelper = outputHelper;
    }

    [Theory]
    [MemberData(nameof(GetDivergingVerseData))]
    public void BibleBookReferenceValidation(int bookId, int expectedDifferenceCount)
    {
        var chapters = BibleChapter.GetChapters().Where(x => x.BibleBookId == bookId).ToList();
        // 1. Flatten the list into individual verse strings for Hebrew
        var hebVerses = chapters.SelectMany(c =>
            Enumerable.Range(1, c.VersesHeb)
                .Select(v => new BibleReference(bookId, c.ChapterHeb ?? 0, v).ToString("$"))
        ).ToList();

        // 2. Flatten the list for LXX
        var lxxVerses = chapters.SelectMany(c =>
            Enumerable.Range(1, c.VersesLxx)
                .Select(v => new BibleReference(bookId, c.ChapterLxx ?? 0, v).ToString("$"))
        ).ToList();

        // 3. Zip them together and print where they diverge
        var mappings = hebVerses.Zip(lxxVerses, (heb, lxx) => new { heb, lxx });
        var divergingList = new List<string>();

        foreach (var map in mappings)
        {
            if (map.heb == map.lxx)
            {
                continue;
            }

            _outputHelper.WriteLine($"{map.heb} || {map.lxx}");
            divergingList.Add($"{map.heb} || {map.lxx}");
        }

        divergingList.Count.ShouldBe(expectedDifferenceCount);
    }

    [Fact]
    public void PsalmsReferenceValidation()
    {
        var bookId = 19;
        var divergingPsalms = BibleChapter.GetChapters().Where(x => x.BibleBookId == bookId && x.VersesHeb != x.VersesLxx).ToList();
        var divergingList = new List<string>();

        foreach (var chapter in divergingPsalms)
        {
            var maxVerses = Math.Max(chapter.VersesHeb, chapter.VersesLxx);

            for (var v = 1; v <= maxVerses; v++)
            {
                // Determine if the verse exists in both versions
                var hebPart = v <= chapter.VersesHeb ? $"Ps {chapter.ChapterHeb,-3},{v}" : "Missing";
                var lxxPart = v <= chapter.VersesLxx ? $"Ps {chapter.ChapterLxx,-3},{v}" : "Missing";

                // If they are different (which they will be for the trailing verses), log or assert
                if (hebPart == lxxPart)
                {
                    continue;
                }

                _outputHelper.WriteLine($"Heb: {hebPart,-20} - Lxx: {lxxPart}");
                divergingList.Add(lxxPart);
            }
        }

        divergingList.Count.ShouldBe(67);
    }

    public static IEnumerable<TheoryDataRow<int, int>> GetDivergingVerseData()
    {
        yield return new(01, 33);
        yield return new(02, 63);
        yield return new(03, 0);
        yield return new(04, 45);
        yield return new(05, 188);
        yield return new(06, 0);
        yield return new(07, 0);
        yield return new(08, 0);
        yield return new(09, 235);
        yield return new(10, 44);
        yield return new(11, 32);
        yield return new(12, 22);
        yield return new(13, 306);
        yield return new(14, 33);
        yield return new(15, 0);
        yield return new(16, 232);
        yield return new(17, 0);
        yield return new(18, 50);
        //yield return new(19, 2434);
        yield return new(20, 0);
        yield return new(21, 20);
        yield return new(22, 14);
        yield return new(23, 70);
        yield return new(24, 26);
        yield return new(25, 0);
        yield return new(26, 0);
        yield return new(27, 37);
        yield return new(28, 41);
        yield return new(29, 26);
        yield return new(30, 0);
        yield return new(31, 0);
        yield return new(32, 0);
        yield return new(33, 0);
        yield return new(34, 14);
        yield return new(35, 0);
        yield return new(36, 0);
        yield return new(37, 0);
        yield return new(38, 17);
        yield return new(39, 6);
    }
}
