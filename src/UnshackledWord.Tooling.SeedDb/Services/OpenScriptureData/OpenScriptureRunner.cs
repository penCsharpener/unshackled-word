using System.Text;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Extensions.Options;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Models.Settings;
using UnshackledWord.Tooling.SeedDb.Services.Abstractions;

namespace UnshackledWord.Tooling.SeedDb.Services.OpenScriptureData;

public class OpenScriptureRunner : IRunner
{
    private readonly OpenScriptureHebrewStrategy _strategy;
    private readonly IFileDownloader _fileDownloader;

    public OpenScriptureRunner(OpenScriptureHebrewStrategy strategy, OpenScriptureHebrewDownloader downloader)
    {
        _strategy = strategy;
        _fileDownloader = downloader;
    }

    public async Task Run(CancellationToken token = default)
    {
        var files = await _fileDownloader.DownloadFileAsync(token);

        foreach (var file in files)
        {
            await _strategy.SaveToDatabase(file, token);
        }
    }
}

public sealed class OpenScriptureHebrewStrategy : IFileParserStrategy
{
    private readonly IDbWriter _writer;
    private readonly IFileService _fileService;
    private readonly AppSettings _option;

    public OpenScriptureHebrewStrategy(IDbWriter writer, IFileService fileService, IOptions<AppSettings> options)
    {
        _writer = writer;
        _fileService = fileService;
        _option = options.Value;
    }

    public async Task SaveToDatabase(string filePath, CancellationToken token = default)
    {
        var xmlText = await _fileService.ReadAllTextAsync(filePath, Encoding.UTF8, token);

        await using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.None);

        var book = await Parse(fileStream, token);
    }

    public static async Task<BookNode> Parse(Stream xmlPath, CancellationToken token = default)
    {
        XDocument doc = await XDocument.LoadAsync(xmlPath, LoadOptions.None, token);
        XNamespace ns = "http://www.bibletechnologies.net/2003/OSIS/namespace";

        var bookElem = doc.Descendants(ns + "div")
            .FirstOrDefault(d => d.Attribute("type")?.Value == "book");

        var book = new BookNode();

        foreach (var chapterElem in bookElem.Elements(ns + "chapter"))
        {
            var chapter = new ChapterNode();

            foreach (var verseElem in chapterElem.Elements(ns + "verse"))
            {
                var wordCounter = 0;
                var previewsWasMaqqef = false;
                var referenceElements = verseElem.Attribute("osisID")?.Value.Split(".")!;
                var verse = new VerseNode();

                foreach (var node in verseElem.Nodes())
                {
                    if (node is not XElement e)
                    {
                        continue;
                    }

                    if (previewsWasMaqqef is false)
                    {
                        wordCounter++;
                    }
                    else
                    {
                        previewsWasMaqqef = false;
                    }

                    if (e.Name == ns + "w")
                    {
                        verse.Words.Add(new WordNode
                        {
                            BookName = referenceElements[0],
                            Chapter = int.Parse(referenceElements[1]),
                            Verse = int.Parse(referenceElements[2]),
                            Lemma = e.Attribute("lemma")?.Value,
                            Morph = e.Attribute("morph")?.Value,
                            Id = e.Attribute("id")?.Value,
                            N = e.Attribute("n")?.Value,
                            Order = wordCounter,
                            Text = e.Value
                        });
                    }

                    if (e.Name == ns + "seg")
                    {
                        previewsWasMaqqef = e.Attribute("type")?.Value == "x-maqqef";

                        verse.Words.Add(new Seg
                        {
                            BookName = referenceElements[0],
                            Chapter = int.Parse(referenceElements[1]),
                            Verse = int.Parse(referenceElements[2]),
                            Text = e.Value,
                            Type = e.Attribute("type")?.Value,
                            Order = wordCounter,
                        });
                    }
                }

                chapter.Verses.Add(verse);
            }

            book.Chapters.Add(chapter);
        }

        return book;
    }
}

public class Seg : WordBase
{
    public string Type { get; set; }
}

public class WordBase
{
    public string BookName { get; set; }
    public int Chapter { get; set; }
    public int Verse { get; set; }
    public int Order { get; set; }
    public string Text { get; set; }
}

public class WordNode : WordBase
{
    public string Lemma { get; set; }
    public string Morph { get; set; }
    public string Id { get; set; }
    public string N { get; set; }
}

public class VerseNode
{
    public List<WordBase> Words { get; set; } = new();
}

public class ChapterNode
{
    public List<VerseNode> Verses { get; set; } = new();
}

public class BookNode
{
    public List<ChapterNode> Chapters { get; set; } = new();
}
