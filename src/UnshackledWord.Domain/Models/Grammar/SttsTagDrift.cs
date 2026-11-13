namespace UnshackledWord.Domain.Models.Grammar;

/// <summary>
/// Compares the in-code STTS list (<see cref="ElbLemmaValueMaps"/>) with the "SttsTags" table, which is the source of truth.
/// Used by the drift unit test (CSV rows) and by the API startup check (database rows).
/// </summary>
public static class SttsTagDrift
{
    /// <summary>One line per difference (missing tag on either side, differing label); empty when both agree.</summary>
    public static IReadOnlyList<string> Compare(IReadOnlyList<ValueLabel> inCode, IEnumerable<SttsTag> table)
    {
        var rows = table.ToDictionary(t => t.Tag, StringComparer.Ordinal);
        var code = inCode.ToDictionary(v => v.Code, StringComparer.Ordinal);
        var differences = new List<string>();

        foreach (var (tag, value) in code.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            if (!rows.TryGetValue(tag, out var row))
            {
                differences.Add($"{tag}: only in code");
                continue;
            }

            if (row.LabelDe != value.LabelDe) differences.Add($"{tag}: labelDe differs (code '{value.LabelDe}', table '{row.LabelDe}')");
            if (row.LabelEn != value.LabelEn) differences.Add($"{tag}: labelEn differs (code '{value.LabelEn}', table '{row.LabelEn}')");
        }

        differences.AddRange(rows.Keys.Where(t => !code.ContainsKey(t)).Order(StringComparer.Ordinal).Select(t => $"{t}: only in table"));
        return differences;
    }
}
