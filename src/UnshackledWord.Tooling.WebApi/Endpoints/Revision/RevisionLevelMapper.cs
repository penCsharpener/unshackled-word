using UnshackledWord.Domain.Models.Dbo.Revision;
using UnshackledWord.Domain.WebApi.Revision;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Revision;

public static class RevisionLevelMapper
{
    /// <summary>Sorted by level; <paramref name="locale"/> is "de" for the German name, anything else gives the English one.</summary>
    public static List<RevisionLevelEntry> ToEntries(IEnumerable<RevisionLevelDbo> levels, string locale) =>
        levels.OrderBy(l => l.Level).Select(l => new RevisionLevelEntry
        {
            Level = l.Level,
            Code = l.Code,
            Name = locale == "de" ? l.NameDe : l.NameEn,
            IsBase = l.IsBase,
            IsDefault = l.IsDefault,
        }).ToList();
}
