namespace UnshackledWord.Tooling.BibleTagger.Features.Configuration;

public sealed class CoreApiSettings
{
    public string BaseUrl { get; set; } = default!;
    public int Timeout { get; set; }
}
