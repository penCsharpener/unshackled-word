namespace UnshackledWord.Domain.WebApi.Elberfelder.GetStrongsOccurrences;

public sealed class GetStrongsOccurrencesRequest
{
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 200;

    public string Strongs { get; set; } = string.Empty;

    /// <summary>1-based.</summary>
    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = DefaultPageSize;
}
