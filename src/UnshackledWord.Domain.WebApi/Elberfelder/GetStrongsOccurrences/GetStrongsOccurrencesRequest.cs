namespace UnshackledWord.Domain.WebApi.Elberfelder.GetStrongsOccurrences;

public sealed class GetStrongsOccurrencesRequest
{
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 200;

    public string Strongs { get; set; } = string.Empty;

    /// <summary>1-based.</summary>
    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = DefaultPageSize;

    /// <summary>Optional Elberfelder book (1-66).</summary>
    public int? Book { get; set; }

    /// <summary>Optional German word key (a <c>key</c> of the Strong's summary renderings), compared lower-cased.</summary>
    public string? Rendering { get; set; }

    /// <summary>Optional: "lxx" lists the confirmed Septuagint tags of a Greek Strong's number (roadmap 0028.9); otherwise the NT/OT list.</summary>
    public string? Corpus { get; set; }
}
