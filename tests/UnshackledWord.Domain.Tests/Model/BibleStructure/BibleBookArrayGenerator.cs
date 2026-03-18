using UnshackledWord.Domain.Extensions;
using UnshackledWord.Domain.Models.BibleStructure;

namespace UnshackledWord.Domain.Tests.Model.BibleStructure;

public class BibleBookArrayGenerator
{
    private readonly ITestOutputHelper _outputHelper;

    public BibleBookArrayGenerator(ITestOutputHelper outputHelper)
    {
        _outputHelper = outputHelper;
    }

    [Fact]
    public void Generate_VersePerChapterHeb()
    {
        foreach (var bookGroup in BibleChapter.GetChapters().GroupBy(x => x.BibleBookId))
        {
            _outputHelper.WriteLine($"{{ {bookGroup.Key}, [{bookGroup.Select(x => x.VersesHeb).JoinStrings(",")}] }},");
        }
    }

    [Fact]
    public void Generate_VersePerChapterLxx()
    {
        foreach (var bookGroup in BibleChapter.GetChapters().GroupBy(x => x.BibleBookId))
        {
            _outputHelper.WriteLine($"{{ {bookGroup.Key}, [{bookGroup.Select(x => x.VersesLxx).JoinStrings(",")}] }},");
        }
    }
}
