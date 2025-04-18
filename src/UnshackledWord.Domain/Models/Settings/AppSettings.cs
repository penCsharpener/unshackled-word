namespace UnshackledWord.Domain.Models.Settings;

public class AppSettings
{
    public DatabaseSeedSettings DatabaseSeeding { get; set; } = default!;
}

public sealed class DatabaseSeedSettings
{
    public string FolderLocation { get; set; } = default!;
    public string[] SRFileUrls { get; set; } = default!;
}
