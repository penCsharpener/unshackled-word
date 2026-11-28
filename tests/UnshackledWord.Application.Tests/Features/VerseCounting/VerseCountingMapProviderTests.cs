using UnshackledWord.Application.Features.VerseCounting;
using UnshackledWord.Domain.Models.BibleStructure.References;

namespace UnshackledWord.Application.Tests.Features.VerseCounting;

public class VerseCountingMapProviderTests
{
    [Fact]
    public async Task GetAsync_LoadsOnce_AndReturnsTheSameMap()
    {
        var calls = 0;
        var provider = new VerseCountingMapProvider(_ =>
        {
            calls++;
            return Task.FromResult<IReadOnlyList<VerseCountingRow>>([new VerseCountingRow(39004005, 39003023)]);
        });

        var first = await provider.GetAsync();
        var second = await provider.GetAsync();

        second.ShouldBeSameAs(first);
        calls.ShouldBe(1);
        first.MapStart(39004005).ShouldBe(39003023);
    }

    [Fact]
    public async Task GetAsync_DoesNotCacheAFailedLoad()
    {
        var calls = 0;
        var provider = new VerseCountingMapProvider(_ =>
            ++calls == 1 ? throw new InvalidOperationException("db down") : Task.FromResult<IReadOnlyList<VerseCountingRow>>([]));

        await Should.ThrowAsync<InvalidOperationException>(() => provider.GetAsync());
        (await provider.GetAsync()).MapStart(1).ShouldBe(1);
        calls.ShouldBe(2);
    }

    [Fact]
    public async Task GetAsync_DoesNotCacheAnEmptyLoad()
    {
        // roadmap 0031: on an empty database the reader may ask before the setup page seeds BibleVerseCountingMapping
        var calls = 0;
        var provider = new VerseCountingMapProvider(_ => Task.FromResult<IReadOnlyList<VerseCountingRow>>(
            ++calls == 1 ? [] : [new VerseCountingRow(39004005, 39003023)]));

        (await provider.GetAsync()).MapStart(39004005).ShouldBe(39004005);
        (await provider.GetAsync()).MapStart(39004005).ShouldBe(39003023);
        (await provider.GetAsync()).MapStart(39004005).ShouldBe(39003023);
        calls.ShouldBe(2);
    }
}
