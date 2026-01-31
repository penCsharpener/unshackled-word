namespace UnshackledWord.Tooling.SeedDb.Services.StepBible.LexiconParser;

public sealed class StepLexiconStrategyFactory
{
    private static StepPersonLexiconEntryParser _person = new();
    private static StepPlaceLexiconEntryParser _place = new();
    private static StepOtherLexiconEntryParser _other = new();
    
    public IStepLexiconParserStrategy<ILexiconEntry<BibleEntity>> GetLexiconStrategy(LineType lineType)
    {
        return lineType switch
        {
            LineType.AnnouncePerson => _person,
            LineType.AnnouncePlace => _place,
            LineType.AnnounceOther => _other,
            _ => throw new ArgumentOutOfRangeException(nameof(lineType), lineType, null)
        };
    }
}
