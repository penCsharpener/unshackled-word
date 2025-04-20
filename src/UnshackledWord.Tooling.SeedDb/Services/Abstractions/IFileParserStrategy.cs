namespace UnshackledWord.Tooling.SeedDb.Services.Abstractions;

/// <summary>
/// Parses the file and save the data to the database
/// </summary>
public interface IFileParserStrategy
{
    Task SaveToDatabase(string filePath, CancellationToken token = default);
}
