using UnshackledWord.Application.Abstractions;
using UnshackledWord.Tooling.WebApi.Endpoints.MetaBible.GetReaderStatus;

namespace UnshackledWord.Tooling.WebApi.Tests;

public class ReaderStatusQueryTests
{
    private static IDbReader ReaderReturning(bool exists)
    {
        var reader = Substitute.For<IDbReader>();
        reader.ExecuteScalarAsync<bool>(Arg.Any<string>(), Arg.Any<object?>()).Returns(exists);
        return reader;
    }

    [Fact]
    public async Task ReadAsync_ReportsNoText_OnAnEmptyDatabase() =>
        (await ReaderStatusQuery.ReadAsync(ReaderReturning(false))).HasText.ShouldBeFalse();

    [Fact]
    public async Task ReadAsync_ReportsText_WhenVersesExist() =>
        (await ReaderStatusQuery.ReadAsync(ReaderReturning(true))).HasText.ShouldBeTrue();

    [Fact]
    public void Sql_OnlyAsksWhetherAVerseExists() =>
        ReaderStatusQuery.HasText.ShouldContain("EXISTS");
}
