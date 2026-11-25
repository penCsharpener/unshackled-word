using UnshackledWord.Domain.Models.Study;
using UnshackledWord.Domain.WebApi.Elberfelder.GetWordDetail;

namespace UnshackledWord.Application.Features.WordDetail;

/// <summary>Pure rule of roadmap 0025 for the word box: which editions a Greek reading shows.</summary>
public static class ReadingEditionsBuilder
{
    /// <summary>
    /// Null when the reading needs no edition section: it is in every main edition and no row has a shift. Only the main editions and shifts decide;
    /// a non-main token (manuscript, KJV, ...) never sets the section on its own, but is listed in <c>Present</c> when the section is shown.
    /// <c>Present</c> = all rows of the word by catalog sort; <c>Missing</c> = main editions without a row, by catalog sort.
    /// </summary>
    public static ReadingEditionsResponse? Build(IReadOnlyCollection<GreekWordEditionRow> rows, IReadOnlyCollection<GreekEditionRow> mainEditions)
    {
        var presentMain = rows.Where(r => r.IsMain).Select(r => r.Code).ToHashSet(StringComparer.Ordinal);
        var missing = mainEditions
            .Where(e => !presentMain.Contains(e.Code))
            .OrderBy(e => e.Sort)
            .Select(e => new EditionRefResponse { Code = e.Code, Kind = e.Kind })
            .ToList();
        var hasShift = rows.Any(r => (r.Shift is not null && r.Shift != 0) || r.ShiftRaw is not null);
        if (missing.Count == 0 && !hasShift)
        {
            return null;
        }

        return new ReadingEditionsResponse
        {
            Present = rows
                .OrderBy(r => r.Sort)
                .ThenBy(r => r.Code, StringComparer.Ordinal)
                .Select(r => new EditionRefResponse
                {
                    Code = r.Code,
                    Kind = r.Kind,
                    Shift = r.Shift is null or 0 ? null : r.Shift,
                    ShiftRaw = r.ShiftRaw,
                })
                .ToList(),
            Missing = missing,
        };
    }
}
