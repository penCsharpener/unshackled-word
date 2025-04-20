using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Models.BibleStructure;
using UnshackledWord.Tooling.SeedDb.Services.Abstractions;

namespace UnshackledWord.Tooling.SeedDb.Services.ElberfelderParser;

public sealed class ElbParserStrategy : IFileParserStrategy
{
    private readonly IFileService _fileService;
    private readonly ParseHelper _parseHelper;
    private static Regex _fillReplace = new(@"(</gr>)(\s|\s+[^<]{2,}?|[,.;:]?\s?\w*\s?)(<gr\s)", RegexOptions.Compiled | RegexOptions.Singleline);
    private static Regex _fillReplace2 = new(@"(<VERS vnumber.*?>)(.*?\s?)(<gr\s|<STYLE\s|<DIV>|</VERS>)", RegexOptions.Compiled | RegexOptions.Singleline);

    public ElbParserStrategy(IFileService fileService, ParseHelper parseHelper)
    {
        _fileService = fileService;
        _parseHelper = parseHelper;
    }

    public async Task SaveToDatabase(string filePath, CancellationToken token = default)
    {
        var xml = await _fileService.ReadAllTextAsync(filePath, token);
        var verseList = new List<ElbVerse>();
        xml = _fillReplace.Replace(xml, "$1<fill>$2</fill>$3");
        xml = _fillReplace2.Replace(xml, "$1<fill>$2</fill>$3");
        await _fileService.WriteAllTextAsync(filePath, xml, Encoding.UTF8, token);
        var bibleBook = Parse(xml);

        foreach (var book in bibleBook.Books)
        {
            foreach (var chapter in book.Chapters)
            {
                foreach (var verse in chapter.Verses)
                {
                    var words = new List<ElbWord>();

                    foreach (var element in verse.Elements)
                    {
                        switch (element)
                        {
                            case WordElement wordElement:
                                words.Add(new(wordElement.Text, wordElement.StrongNumber, null));
                                break;
                            case FillTextElement fillTextElement:
                                words.Add(new(fillTextElement.Text, null, null));
                                break;
                            case StyleElement styleElement:
                                words.Add(new (styleElement.Text, null, null));
                                break;
                            case BreakElement breakElement:
                                break;
                        }
                    }

                    var verseText = string.Join(" ", words.Select(x => x.Word))
                        .Replace("  ", " ").Trim()
                        .Replace(" ,", ",")
                        .Replace(" .", ".")
                        .Replace(" :", ":")
                        .Replace(" ;", ";")
                        .Replace(" !", "!")
                        .Replace(" ?", "?");

                    var srVerse = new ElbVerse(BibleBook.NewTestamentBooks[book.BookNumber], chapter.ChapterNumber, verse.VerseNumber, verseText, words);

                    if (srVerse is { BibleBook: { Id: 63 }, ChapterId: 1, VerseId: 1 })
                    {
                        Console.WriteLine();
                    }

                    verseList.Add(srVerse);
                }
            }
        }
    }

    public static XmlBible Parse(string xmlContent)
    {
        var doc = XDocument.Parse(xmlContent);
        var root = doc.Element("XMLBIBLE");

        var xmlBible = new XmlBible
        {
            BibleName = root.Attribute("biblename")?.Value,
            Revision = root.Attribute("revision")?.Value,
            Type = root.Attribute("type")?.Value,
            Information = ParseInformation(root.Element("INFORMATION"))
        };

        foreach (var bookElem in root.Elements("BIBLEBOOK"))
        {
            var book = new XmlBibleBook
            {
                BookNumber = int.Parse(bookElem.Attribute("bnumber")?.Value ?? "0"),
                BookName = bookElem.Attribute("bname")?.Value,
                ShortName = bookElem.Attribute("bsname")?.Value
            };

            foreach (var chapterElem in bookElem.Elements("CHAPTER"))
            {
                var chapter = new Chapter
                {
                    ChapterNumber = int.Parse(chapterElem.Attribute("cnumber")?.Value ?? "0")
                };

                foreach (var verseElem in chapterElem.Elements("VERS"))
                {
                    var verse = new Verse
                    {
                        VerseNumber = int.Parse(verseElem.Attribute("vnumber")?.Value ?? "0"),
                        Elements = ParseVerseElements(verseElem),
                        Text = verseElem.Value
                    };

                    chapter.Verses.Add(verse);
                }

                book.Chapters.Add(chapter);
            }

            xmlBible.Books.Add(book);
        }

        return xmlBible;
    }

    private static BibleInformation ParseInformation(XElement info)
    {
        return new BibleInformation
        {
            Title = info.Element("title")?.Value,
            Creator = info.Element("creator")?.Value,
            Description = info.Element("description")?.Value,
            Publisher = info.Element("publisher")?.Value,
            Date = DateTime.TryParse(info.Element("date")?.Value, out var date) ? date : default,
            Format = info.Element("format")?.Value,
            Identifier = info.Element("identifier")?.Value,
            Language = info.Element("language")?.Value,
            Rights = info.Element("rights")?.Value,
            Source = info.Element("source")?.Value
        };
    }

    private static List<BibleElement> ParseVerseElements(XElement verseElem)
    {
        var elements = new List<BibleElement>();

        foreach (var node in verseElem.Nodes())
        {
            switch (node)
            {
                case XText text:
                    var trimmed = text.Value.Trim();
                    if (!string.IsNullOrWhiteSpace(trimmed))
                        elements.Add(new PlainTextElement { Text = trimmed });
                    break;

                case XElement gr when gr.Name == "gr":
                    elements.Add(new WordElement
                    {
                        StrongNumber = gr.Attribute("str")?.Value,
                        Text = gr.Value.Trim()
                    });
                    break;

                case XElement gr when gr.Name == "fill":
                    elements.Add(new FillTextElement
                    {
                        Text = gr.Value.Trim()
                    });
                    break;

                case XElement style when style.Name == "STYLE":
                    elements.Add(new StyleElement()
                    {
                        Css = style.Attribute("css")?.Value,
                        Text = style.Value.Trim()
                    });
                    break;

                case XElement div when div.Name == "DIV":
                    var note = div.Element("NOTE");
                    if (note != null)
                    {
                        elements.Add(new NoteElement
                        {
                            Type = note.Attribute("type")?.Value,
                            Text = note.Value.Trim()
                        });
                    }
                    break;

                case XElement br when br.Name == "BR":
                    elements.Add(new BreakElement
                    {
                        Art = br.Attribute("art")?.Value
                    });
                    break;
            }
        }

        return elements;
    }
}
