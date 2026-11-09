using UnshackledWord.TestTooling;

namespace UnshackledWord.Infrastructure.Tests;

public class FakeDbReaderTests
{
    [Fact]
    public async Task ReadAsListAsync_ReturnsQueuedRowsInOrder_AndRecordsSql()
    {
        var reader = new FakeDbReader().Returns(1, 2).Returns(3);

        var first = await reader.ReadAsListAsync<int>("SELECT 1", new { WordId = 7 });
        var second = await reader.ReadAsListAsync<int>("SELECT 2");

        first.ShouldBe([1, 2]);
        second.ShouldBe([3]);
        reader.Calls.Count.ShouldBe(2);
        reader.Calls[0].Sql.ShouldBe("SELECT 1");
        reader.LastSql.ShouldBe("SELECT 2");
    }

    [Fact]
    public async Task LastParam_ReadsPropertyOfParameterObject()
    {
        var reader = new FakeDbReader();

        await reader.ReadAsListAsync<int>("SELECT 1", new { WordId = 7 });

        reader.Calls.Count.ShouldBe(1);
        reader.LastParam("WordId").ShouldBe(7);
        Should.Throw<InvalidOperationException>(() => reader.LastParam("Missing"));
    }

    [Fact]
    public async Task ReadFirstOrDefaultAsync_ReturnsFirstRowOrDefault()
    {
        var reader = new FakeDbReader().Returns("a", "b");

        (await reader.ReadFirstOrDefaultAsync<string>("SELECT")).ShouldBe("a");
        (await reader.ReadFirstOrDefaultAsync<string>("SELECT")).ShouldBeNull();
    }

    [Fact]
    public async Task ReadMultipleAsListAsync_IsNotSupported()
    {
        await Should.ThrowAsync<NotSupportedException>(() =>
            new FakeDbReader().ReadMultipleAsListAsync<int>("SELECT", null, _ => Task.FromResult(new List<int>())));
    }
}
