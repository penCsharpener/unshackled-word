namespace UnshackledWord.Domain.WebApi.DataQuality;

// Request/response bodies of /api/dataQuality/* (data quality page). camelCase JSON, nulls omitted.
// Contract: docs/drafts/data-quality-design.md "API contract". Additions to the draft are marked "added".

/// <summary>Facets of every GET: testament (ot / nt) and book.</summary>
public class DqFacetsRequest
{
    public string? Testament { get; set; }
    public int? BookId { get; set; }
}

public sealed class OverviewRequest : DqFacetsRequest;

public sealed class OverviewResponse
{
    public DateTime? RefreshedAt { get; set; }
    public bool Refreshing { get; set; }
    public DateTime? LastEditAt { get; set; }
    public OverviewTotals Totals { get; set; } = new();
    public List<OverviewCheck> Checks { get; set; } = [];
}

public sealed class OverviewTotals
{
    public int GermanWords { get; set; }
    public int SourceWords { get; set; }
}

public sealed class OverviewCheck
{
    public string Code { get; set; } = string.Empty;
    public int Tier { get; set; } = 1;
    public int Count { get; set; }
    public int ErrorCount { get; set; }
    public int? PreviousCount { get; set; }
    public KeyCount? Sub { get; set; }
}

public sealed class KeyCount
{
    public string Key { get; set; } = string.Empty;
    public int Count { get; set; }
}

public sealed class UnmappedRequest : DqFacetsRequest
{
    /// <summary>german | greek | hebrew (route).</summary>
    public string Side { get; set; } = string.Empty;

    /// <summary>true (default): top lists and the book chart only count probable gaps (severity error); false: everything.</summary>
    public bool Content { get; set; } = true;

    public int Top { get; set; } = 10;
}

public sealed class UnmappedResponse
{
    public string Side { get; set; } = string.Empty;
    public int Total { get; set; }
    public DateTime? RefreshedAt { get; set; }
    public List<UnmappedClassCount> ByClass { get; set; } = [];
    public List<BookCount> ByBook { get; set; } = [];
    public List<UnmappedTop> Top { get; set; } = [];
}

public sealed class UnmappedClassCount
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int Count { get; set; }
    public bool Legit { get; set; }
}

public sealed class BookCount
{
    public int BibleBookId { get; set; }
    public int Count { get; set; }
}

public sealed class UnmappedTop
{
    /// <summary>lemma~PoS (german) or base Strong's (greek / hebrew): the explorer's q.</summary>
    public string Key { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;
    public string? Original { get; set; }
    public string? Transliteration { get; set; }
    public string? Gloss { get; set; }
    public string? Pos { get; set; }
    public int Count { get; set; }
}

public sealed class CoverageRequest : DqFacetsRequest
{
    /// <summary>german (default; share of unaligned German content words) | source (share of unaligned original content words).</summary>
    public string Metric { get; set; } = "german";
}

public sealed class CoverageResponse
{
    public string Metric { get; set; } = string.Empty;
    public DateTime? RefreshedAt { get; set; }
    public List<CoverageBook> Books { get; set; } = [];
}

public sealed class CoverageBook
{
    public int BibleBookId { get; set; }
    public List<CoverageChapter> Chapters { get; set; } = [];
}

public sealed class CoverageChapter
{
    public int Chapter { get; set; }
    public int Total { get; set; }
    public int Unaligned { get; set; }
}

public sealed class CheckRequest : DqFacetsRequest
{
    /// <summary>Check code (route): U1 U2 U3 C1 M1 M2 M3 M4 L1 L2 L3 L4 L5.</summary>
    public string Code { get; set; } = string.Empty;

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public string? Pos { get; set; }
    public string? Lang { get; set; }

    /// <summary>Original class key (U2/U3: also notNaTr, HTo, ketiv; M3: G:N / H:N or N with lang); L5: the field.</summary>
    public string? Class { get; set; }

    public int? Chapter { get; set; }
    public int? MinCount { get; set; }

    /// <summary>Aggregated checks: expands this group (the group's key) into its findings.</summary>
    public string? Group { get; set; }
}

public sealed class CheckResponse
{
    public string Code { get; set; } = string.Empty;
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public DateTime? RefreshedAt { get; set; }
    public List<CheckGroup>? Groups { get; set; }
    public CheckMatrix? Matrix { get; set; }
    public List<Finding> Items { get; set; } = [];
}

public sealed class CheckGroup
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int Count { get; set; }
    public double? Share { get; set; }
    public List<string>? Flags { get; set; }

    /// <summary>added: G / H when the group is language specific (M1); pass as lang together with group.</summary>
    public string? Lang { get; set; }
}

public sealed class CheckMatrix
{
    public List<string> Rows { get; set; } = [];
    public List<string> Columns { get; set; } = [];
    public List<MatrixCell> Cells { get; set; } = [];
}

public sealed class MatrixCell
{
    public string Row { get; set; } = string.Empty;
    public string Column { get; set; } = string.Empty;
    public int Count { get; set; }
    public bool Expected { get; set; }
}

public sealed class Finding
{
    public string Check { get; set; } = string.Empty;
    public string Severity { get; set; } = "info";
    public int BibleBookId { get; set; }
    public int Chapter { get; set; }
    public int Verse { get; set; }
    public int? ElbWordId { get; set; }
    public int? StepWordId { get; set; }
    public string? Lang { get; set; }
    public string? German { get; set; }
    public string? Lemma { get; set; }
    public string? Pos { get; set; }
    public string? Original { get; set; }
    public string? Strongs { get; set; }
    public string? Gloss { get; set; }
    public string Detail { get; set; } = string.Empty;
    public string? GroupKey { get; set; }

    /// <summary>added: about five words of verse text left and right of the German word.</summary>
    public string? Context { get; set; }
}

public sealed class OriginalSearchRequest
{
    public string Q { get; set; } = string.Empty;
    public int Limit { get; set; } = 20;
}

public sealed class OriginalSearchResponse
{
    public string Q { get; set; } = string.Empty;
    public string Folded { get; set; } = string.Empty;
    public List<OriginalCandidate> Candidates { get; set; } = [];
}

public sealed class OriginalCandidate
{
    public string Strongs { get; set; } = string.Empty;
    public string Lang { get; set; } = string.Empty;
    public string Lemma { get; set; } = string.Empty;
    public string Transliteration { get; set; } = string.Empty;
    public string Gloss { get; set; } = string.Empty;
    public int Occurrences { get; set; }
    public string MatchedBy { get; set; } = string.Empty;
}

public sealed class OriginalDetailRequest
{
    public string Strongs { get; set; } = string.Empty;
}

public sealed class OriginalDetailResponse
{
    public string Strongs { get; set; } = string.Empty;
    public string Lang { get; set; } = string.Empty;
    public string Lemma { get; set; } = string.Empty;
    public string Transliteration { get; set; } = string.Empty;
    public string Gloss { get; set; } = string.Empty;
    public string OrigClass { get; set; } = string.Empty;
    public int Occurrences { get; set; }
    public int Aligned { get; set; }
    public int Unmapped { get; set; }
    public DateTime? RefreshedAt { get; set; }
    public List<Rendering> Renderings { get; set; } = [];
}

public sealed class Rendering
{
    public string Lemma { get; set; } = string.Empty;
    public string Pos { get; set; } = string.Empty;
    public int Count { get; set; }
    public double Share { get; set; }
    public List<string> Flags { get; set; } = [];
}

public sealed class GermanSearchRequest
{
    public string Q { get; set; } = string.Empty;
    public string? Pos { get; set; }
    public int Limit { get; set; } = 20;
}

public sealed class GermanSearchResponse
{
    public string Q { get; set; } = string.Empty;
    public List<GermanCandidate> Candidates { get; set; } = [];
}

public sealed class GermanCandidate
{
    public string Lemma { get; set; } = string.Empty;
    public string Pos { get; set; } = string.Empty;
    public int Count { get; set; }
    public string MatchedBy { get; set; } = string.Empty;
}

public sealed class GermanDetailRequest
{
    public string Lemma { get; set; } = string.Empty;
    public string? Pos { get; set; }
}

public sealed class GermanDetailResponse
{
    public string Lemma { get; set; } = string.Empty;
    public string? Pos { get; set; }
    public int Count { get; set; }
    public DateTime? RefreshedAt { get; set; }
    public List<GermanSource> Sources { get; set; } = [];
    public List<GermanForm> Forms { get; set; } = [];
}

public sealed class GermanSource
{
    /// <summary>G2316, H0430, added or none.</summary>
    public string Key { get; set; } = string.Empty;

    public string? Lang { get; set; }
    public string? Lemma { get; set; }
    public string? Transliteration { get; set; }
    public string? Gloss { get; set; }
    public string? OrigClass { get; set; }
    public int Count { get; set; }
    public double Share { get; set; }
    public List<string> Flags { get; set; } = [];
}

public sealed class GermanForm
{
    public string Form { get; set; } = string.Empty;
    public int Count { get; set; }
    public List<OtherLemma> OtherLemmas { get; set; } = [];
}

public sealed class OtherLemma
{
    public string Lemma { get; set; } = string.Empty;
    public string Pos { get; set; } = string.Empty;
    public int Count { get; set; }
}

public sealed class HitsRequest
{
    public string? Strongs { get; set; }
    public string? Lemma { get; set; }
    public string? Pos { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public sealed class HitsResponse
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<Finding> Items { get; set; } = [];
}

public sealed class RefreshResponse
{
    public bool Refreshing { get; set; }
}
