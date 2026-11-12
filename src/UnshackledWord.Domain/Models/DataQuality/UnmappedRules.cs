namespace UnshackledWord.Domain.Models.DataQuality;

public readonly record struct UnmappedClass(string Key, bool Legit, string Severity);

/// <summary>
/// Classification of words without a counterpart (checks U1–U3): which of them are legitimate (article, object marker,
/// words missing from NA/TR, Ketiv) and which are probable gaps.
/// </summary>
public static class UnmappedRules
{
    public const string Error = "error";
    public const string Warning = "warning";
    public const string Info = "info";

    public const string NotInNaTr = "notNaTr";
    public const string ObjectMarker = "HTo";
    public const string Ketiv = "ketiv";
    public const string ObjectMarkerStrongs = "H0853";

    /// <summary>German word without an original word. Content words and words without any mapping row are errors.</summary>
    public static string GermanSeverity(string? pos, string kind) =>
        kind == "none" || PosClassRules.IsContentGermanPos(pos) ? Error : Info;

    /// <summary>
    /// Class key of an original word without German: the special slices first (not in NA/TR, Ketiv, object marker),
    /// else the class itself. Mirrors the SQL expression in the repository (<see cref="SourceClassKeySql"/>).
    /// </summary>
    public static string SourceClassKey(string lang, string? origClass, string? strongsBase, string? hebType, bool? inNa, bool? inTr)
    {
        if (lang == PosClassRules.Greek) return inNa != true && inTr != true ? NotInNaTr : origClass ?? "?";
        if (hebType is not null && hebType.StartsWith('Q')) return Ketiv;
        if (strongsBase == ObjectMarkerStrongs) return ObjectMarker;
        return origClass ?? "?";
    }

    /// <summary>The same expression as <see cref="SourceClassKey"/> for SQL over DqWordFacts (alias f).</summary>
    public const string SourceClassKeySql = """
        CASE WHEN f."Lang" = 'G' THEN CASE WHEN NOT COALESCE(f."InNa", false) AND NOT COALESCE(f."InTr", false) THEN 'notNaTr' ELSE COALESCE(f."OrigClass", '?') END
             WHEN f."HebType" LIKE 'Q%' THEN 'ketiv'
             WHEN f."StrongsBase" = 'H0853' THEN 'HTo'
             ELSE COALESCE(f."OrigClass", '?') END
        """;

    public static UnmappedClass DescribeSource(string lang, string key)
    {
        if (key is NotInNaTr or ObjectMarker or Ketiv) return new UnmappedClass(key, true, Info);
        if (lang == PosClassRules.Greek && key == "T") return new UnmappedClass(key, true, Info);
        return new UnmappedClass(key, false, PosClassRules.IsContentClass(lang, key) ? Error : Warning);
    }

    /// <summary>Class keys that are errors, for a SQL <c>= ANY</c> filter ("content words only").</summary>
    public static string[] ErrorKeys(string lang) => lang == PosClassRules.Greek ? PosClassRules.GreekContentClasses : PosClassRules.HebrewContentClasses;
}
