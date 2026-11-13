using System.Text.Json;
using UnshackledWord.Domain.Models.Study;

namespace UnshackledWord.Application.Features.StrongsStats;

/// <summary>Reads the BookCounts jsonb of the Stats* views and sums them over rows.</summary>
public static class SnapshotBookCounts
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public static IReadOnlyList<SnapshotBookCount> Parse(string? json)
    {
        return string.IsNullOrWhiteSpace(json) ? [] : JsonSerializer.Deserialize<List<SnapshotBookCount>>(json, Options) ?? [];
    }

    /// <summary>Sums the books of several rows (match keys or PoS rows), ascending by book, restricted to a book range.</summary>
    public static List<SnapshotBookCount> Merge(IEnumerable<string?> jsons, int firstBook = 1, int lastBook = int.MaxValue)
    {
        return jsons
            .SelectMany(Parse)
            .Where(b => b.B >= firstBook && b.B <= lastBook)
            .GroupBy(b => b.B)
            .Select(g => new SnapshotBookCount { B = g.Key, C = g.Sum(b => b.C), V = g.Sum(b => b.V), U = g.Sum(b => b.U) })
            .OrderBy(b => b.B)
            .ToList();
    }
}
