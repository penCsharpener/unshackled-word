using UnshackledWord.Tooling.SeedDb.Services.Abstractions;

namespace UnshackledWord.Tooling.SeedDb.Services.ElberfelderParser;

public class HyphenFixVerseRunner : IRunner
{
    private readonly HyphenTypoDetectionService _detectionService;

    public HyphenFixVerseRunner(HyphenTypoDetectionService detectionService)
    {
        _detectionService = detectionService;
    }

    public async Task Run(CancellationToken token = default)
    {
        var findings = await _detectionService.GetHyphenWordsAsync(token);

        await _detectionService.FixHyphenWordsAsync(findings, token);
    }
}
