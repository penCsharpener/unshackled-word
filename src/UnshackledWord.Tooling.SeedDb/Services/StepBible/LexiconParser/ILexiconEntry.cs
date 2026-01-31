namespace UnshackledWord.Tooling.SeedDb.Services.StepBible.LexiconParser;

public interface ILexiconEntry<T> where T : BibleEntity
{
    public T Entity { get; set; }
    public string? Note { get; set; }
    public string? Briefest { get; set; }
    public string Brief { get; set; }
    public string Short { get; set; }
    public string Article { get; set; }
}