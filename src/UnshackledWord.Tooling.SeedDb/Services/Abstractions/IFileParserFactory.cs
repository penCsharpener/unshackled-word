namespace UnshackledWord.Tooling.SeedDb.Services.Abstractions;

public interface IFileParserFactory
{
    IFileParserStrategy GetParser(string filePath);
}
