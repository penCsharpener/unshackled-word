using UnshackledWord.Domain.Models.Study;

namespace UnshackledWord.Application.Features.StrongsStats;

public static class StrongsMatchKeys
{
    /// <summary>
    /// The keys a request matches in the STEP word tables. A number with a letter ("G3004G") matches only itself, a number
    /// without one ("G3004") matches every lexicon variant. The normalized request is always included. Sorted, distinct.
    /// </summary>
    public static string[] Resolve(string normalized, StrongsNumber parsed, IEnumerable<LexiconEntry> entries)
    {
        return entries
            .Select(e => new StrongsNumber(parsed.Language, e.Number, e.Extra).ToString())
            .Append(normalized)
            .Distinct()
            .Order(StringComparer.Ordinal)
            .ToArray();
    }
}
