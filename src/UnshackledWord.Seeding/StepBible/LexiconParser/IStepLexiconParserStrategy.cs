namespace UnshackledWord.Seeding.StepBible.LexiconParser;

public interface IStepLexiconParserStrategy<out T> where T : ILexiconEntry<BibleEntity>
{
    ILexiconEntry<BibleEntity> Parse(List<(LineType LineType, string Line)> entryLines);
}