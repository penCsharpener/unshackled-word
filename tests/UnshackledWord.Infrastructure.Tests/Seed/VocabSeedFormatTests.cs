using UnshackledWord.Infrastructure.Seed.Vocab;

namespace UnshackledWord.Infrastructure.Tests.Seed;

/// <summary>The committed vocab seed (<c>assets/seed/vocab</c>) reads and writes back byte for byte, so an export over an unchanged database changes no file.</summary>
public class VocabSeedFormatTests
{
    [Fact]
    public void CommittedSeed_ParsesAndSerializesToTheSameBytes()
    {
        var folder = FindRepoFolder(Path.Combine("assets", "seed", "vocab"));
        Assert.SkipWhen(folder is null, "assets/seed/vocab not found.");

        var files = new[] { VocabSeedExporter.EntriesFile, VocabSeedExporter.KeysFile, VocabSeedExporter.MeaningsFile, VocabSeedExporter.RootsFile, VocabSeedExporter.WordRootsFile }
            .Select(name => File.ReadAllBytes(Path.Combine(folder!, name)))
            .ToArray();
        var data = VocabSeedImporter.Parse(files[0], files[1], files[2], files[3], files[4]);
        var written = VocabSeedExporter.Serialize(data);

        for (var i = 0; i < files.Length; i++)
        {
            written[i].Bytes.ShouldBe(files[i], $"{written[i].Name} differs after parse and serialize");
        }

        data.Entries.GroupBy(e => (e.Track, e.Lemma, e.Homograph)).ShouldAllBe(g => g.Count() == 1);
    }

    private static string? FindRepoFolder(string relative)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "UnshackledWord.slnx")))
            {
                var path = Path.Combine(dir.FullName, relative);

                return Directory.Exists(path) ? path : null;
            }
        }

        return null;
    }
}
