using Microsoft.JSInterop;

namespace UnshackledWord.Tooling.BibleTagger.Components.Pages;

public partial class Elb1871SrTagger
{
    private BibleReference bibleReference = new BibleReference();
    private List<string> bibleBooks = new List<string> { "Genesis", "Exodus", "Leviticus", /* add other books */ };
    private List<WordItem> elberfelderWords = new List<WordItem>();
    private List<WordItem> greekWords = new List<WordItem>();

    private async Task HandleSubmit()
    {
        var BibleService = new BibleService();
        // Fetch words from the database for the selected book, chapter, and verse
        var verseDetails = await BibleService.GetVerseWordsAsync(bibleReference);
        elberfelderWords = verseDetails.ElberfelderWords;
        greekWords = verseDetails.GreekWords;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            // Call JS to initialize the keyboard shortcut after the first render
            await JS.InvokeVoidAsync("initializeKeyboardShortcut");
        }
    }

    private void ToggleSelection(WordItem word)
    {
        word.Selected = !word.Selected;
    }

    private void SaveWordMapping()
    {
        var selectedElberfelderWords = elberfelderWords.Where(w => w.Selected).ToList();
        var selectedGreekWords = greekWords.Where(w => w.Selected).ToList();
        var WordMappingService = new WordMappingService();
        Console.WriteLine("saving");

        foreach (var elWord in selectedElberfelderWords)
        {
            foreach (var grWord in selectedGreekWords)
            {
                WordMappingService.SaveMapping(elWord.Id, grWord.Id);
            }
        }

        // Deselect all words after saving
        foreach (var word in elberfelderWords)
        {
            word.Selected = false;
        }

        foreach (var word in greekWords)
        {
            word.Selected = false;
        }
    }
}

public class BibleReference
{
    public string Book { get; set; }
    public int Chapter { get; set; }
    public int Verse { get; set; }
}

public class WordItem
{
    public int Id { get; set; }
    public string Text { get; set; }
    public string PartOfSpeech { get; set; }
    public bool Selected { get; set; } // To keep track of selected words
}

public class WordMapping
{
    public int Id { get; set; }
    public int ElberfelderWordId { get; set; }
    public int GreekWordId { get; set; }
}

public class BibleService
{
    public async Task<VerseDetails> GetVerseWordsAsync(BibleReference reference)
    {
        // Make an API call or query the database to get the Elberfelder and Greek words for the specified reference
        // For demonstration purposes:
        return new VerseDetails
        {
            ElberfelderWords = new List<WordItem>
            {
                new WordItem { Id = 1, Text = "In", PartOfSpeech = "Preposition" },
                new WordItem { Id = 2, Text = "the", PartOfSpeech = "Article" },
                // more words...
            },
            GreekWords = new List<WordItem>
            {
                new WordItem { Id = 1, Text = "ἐν", PartOfSpeech = "Preposition" },
                new WordItem { Id = 2, Text = "τῷ", PartOfSpeech = "Article" },
                // more words...
            }
        };
    }
}

public sealed class VerseDetails
{
    public List<WordItem> ElberfelderWords { get; set; }
    public List<WordItem> GreekWords { get; set; }
}

public class WordMappingService
{

    public void SaveMapping(int elberfelderWordId, int greekWordId)
    {
        var mapping = new WordMapping
        {
            ElberfelderWordId = elberfelderWordId,
            GreekWordId = greekWordId
        };
    }
}
