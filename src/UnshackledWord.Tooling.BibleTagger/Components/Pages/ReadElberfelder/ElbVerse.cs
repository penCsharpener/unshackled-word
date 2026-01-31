using UnshackledWord.Domain.WebApi.BibleTagger.Reading;

namespace UnshackledWord.Tooling.BibleTagger.Components.Pages.ReadElberfelder;

public sealed class ElbVerse
{
    public Dictionary<int, WordResponse> Words { get; set; } = [];
}
