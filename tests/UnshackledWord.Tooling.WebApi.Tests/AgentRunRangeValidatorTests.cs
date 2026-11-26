using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.AgentRuns;

namespace UnshackledWord.Tooling.WebApi.Tests;

/// <summary>Roadmap 0028.7f: <c>includeConfirmed</c> and <c>textForm</c> of the agent-run requests are valid only for mode lxx-tag.</summary>
public class AgentRunRangeValidatorTests
{
    private static AgentRunRangeRequest Request(string? mode, string? textForm = null, bool includeConfirmed = false) =>
        new() { Book = 8, FromChapter = 1, FromVerse = 1, Profile = "p", Mode = mode, TextForm = textForm, IncludeConfirmed = includeConfirmed };

    [Theory]
    [InlineData("lxx-tag", null, false, true)]
    [InlineData("lxx-tag", "og", false, true)]
    [InlineData("lxx-tag", "TH", true, true)]
    [InlineData("lxx-tag", "xx", false, false)]
    [InlineData("lxx-tag", "", false, true)]
    [InlineData("redo", "og", false, false)]
    [InlineData(null, null, true, false)]
    [InlineData("fill-open", null, true, false)]
    [InlineData("redo", null, false, true)]
    [InlineData("lemma", null, false, true)]
    public void LxxOptions_Validate(string? mode, string? textForm, bool includeConfirmed, bool valid) =>
        new AgentRunRangeValidator().Validate(Request(mode, textForm, includeConfirmed)).IsValid.ShouldBe(valid);
}
