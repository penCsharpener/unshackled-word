namespace UnshackledWord.Domain.WebApi.MetaBible.ParseReference;

public sealed class ParseReferenceRequest
{
    /// <summary>What the user typed, in German citation style (e.g. "Offb 2,1-5.7.9-10").</summary>
    public string Q { get; set; } = string.Empty;
}
