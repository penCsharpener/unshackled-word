using UnshackledWord.Application.Repositories;
using UnshackledWord.Domain.Models.Dbo.Step;
using UnshackledWord.Domain.Models.Study;
using UnshackledWord.Tooling.McpServer.Tools;

namespace UnshackledWord.Tooling.McpServer.Tests;

public class LexiconToolsTests
{
    private static LexiconEntry Entry(string? extra, string? lexicon = "short text", int language = 2, int number = 26) => new()
    {
        LanguageId = language, Number = number, Extra = extra, OriginalWord = "ἀγάπη", Transliteration = "agapē",
        Morphology = "G:N-F", Gloss = "love", Lexicon = lexicon,
    };

    [Fact]
    public async Task LookupStrongs_ReturnsEntry()
    {
        var repo = Substitute.For<IStrongsLexiconRepository>();
        repo.GetEntriesAsync(new StrongsNumber(StrongsLanguage.Greek, 26, null), Arg.Any<CancellationToken>())
            .Returns(new List<LexiconEntry> { Entry(null) });

        var text = await LexiconTools.LookupStrongs(repo, "G26");

        text.ShouldContain("G0026 (Greek) ἀγάπη [agapē]");
        text.ShouldContain("Gloss: love");
        text.ShouldContain("Lexicon: short text");
    }

    [Fact]
    public async Task LookupStrongs_MultipleVariants_ListsAll()
    {
        var repo = Substitute.For<IStrongsLexiconRepository>();
        repo.GetEntriesAsync(default, default).ReturnsForAnyArgs(new List<LexiconEntry>
        {
            Entry("A", language: 0, number: 1254), Entry("B", language: 0, number: 1254),
        });

        var text = await LexiconTools.LookupStrongs(repo, "H1254");

        text.ShouldContain("H1254A (Hebrew)");
        text.ShouldContain("H1254B (Hebrew)");
    }

    [Fact]
    public async Task LookupStrongs_LongLexicon_ShortenedUnlessFull()
    {
        var repo = Substitute.For<IStrongsLexiconRepository>();
        repo.GetEntriesAsync(default, default).ReturnsForAnyArgs(new List<LexiconEntry> { Entry(null, new string('x', 2000)) });

        (await LexiconTools.LookupStrongs(repo, "G26")).ShouldContain("full=true");
        (await LexiconTools.LookupStrongs(repo, "G26", full: true)).ShouldContain(new string('x', 2000));
    }

    [Fact]
    public async Task LookupStrongs_InvalidOrUnknown_ReturnsText()
    {
        var repo = Substitute.For<IStrongsLexiconRepository>();
        repo.GetEntriesAsync(default, default).ReturnsForAnyArgs(new List<LexiconEntry>());

        (await LexiconTools.LookupStrongs(repo, "26")).ShouldContain("Prefix G");
        (await LexiconTools.LookupStrongs(repo, "G99999")).ShouldContain("No lexicon entry");
    }
}
