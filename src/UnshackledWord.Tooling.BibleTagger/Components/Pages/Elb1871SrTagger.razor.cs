using Microsoft.JSInterop;
using UnshackledWord.Domain.Models.Dbo;
using UnshackledWord.Domain.WebApi.BibleTagger.CreateElbSrMapping;
using UnshackledWord.Domain.WebApi.BibleTagger.GetVerse;

namespace UnshackledWord.Tooling.BibleTagger.Components.Pages;

public partial class Elb1871SrTagger
{
    private BibleReference bibleReference = new BibleReference()
    {
        BookId = 40,
        Chapter = 1,
        Verse = 1
    };
    private List<BibleBookDbo> bibleBooks = new();
    private List<WordItem> elberfelderWords = new List<WordItem>();
    private List<WordItem> greekWords = new List<WordItem>();
    private bool invalidSelection = false;
    private GetVerseResponse verseResponse = new();
    private bool ShowNotification = false;

    public CreateElbSrResponse? MappingResult
    {
        get => field;
        set
        {
            ShowNotification = value is not null;
            field = value;
        }
    }

    private void HideNotification()
    {
        MappingResult = null;
    }

    protected override async Task OnInitializedAsync()
    {
        var books = await MetaRepo.GetBibleBooksAsync(1);
        bibleBooks = books.Where(x => x.Id >= 40).ToList();
        await HandleSubmitAsync();
    }

    private async Task HandleSubmitAsync()
    {
        verseResponse = await ElbRepo.GetVerseAsync(bibleReference.BookId, bibleReference.Chapter, bibleReference.Verse);
        // Fetch words from the database for the selected book, chapter, and verse
        elberfelderWords = verseResponse.ElberfelderWords.Select(x => new WordItem()
        {
            Id = x.Id,
            PartOfSpeech = x.PartOfSpeech,
            Strongs = x.Strongs,
            Text = x.PlainWord!,
            Lemma = x.Lemma
        }).ToList();
        greekWords = verseResponse.SrWords.Select(x => new WordItem()
        {
            Id = x.Id,
            PartOfSpeech = x.PartOfSpeech,
            Strongs = x.Strongs,
            Text = x.WordInContext,
            Lemma = x.Lemma
        }).ToList();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            // Call JS to initialize the keyboard shortcut after the first render
            await Js.InvokeVoidAsync("initializeKeyboardShortcut");
        }
    }

    private void ToggleSelection(WordItem word)
    {
        word.Selected = !word.Selected;
    }

    private async Task SaveWordMappingAsync()
    {
        var selectedElberfelderWords = elberfelderWords.Where(w => w.Selected).ToList();
        var selectedGreekWords = greekWords.Where(w => w.Selected).ToList();

        invalidSelection = selectedElberfelderWords.Count > 1 || selectedGreekWords.Count > 1;

        if (invalidSelection)
        {
            return;
        }

        var elWord = selectedElberfelderWords.First();
        var grWord = selectedGreekWords.First();

        MappingResult = await ElbRepo.CreateMappingAsync(verseResponse.ElberfelderWords.First(x => x.Id == elWord.Id),
            verseResponse.SrWords.First(x => x.Id == grWord.Id));

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
    public int BookId { get; set; }
    public int Chapter { get; set; }
    public int Verse { get; set; }
}

public class WordItem
{
    public int Id { get; set; }
    public string Text { get; set; } = default!;
    public string? Lemma { get; set; } = default!;
    public string? PartOfSpeech { get; set; } = default!;
    public string? Strongs { get; set; } = default!;
    public bool Selected { get; set; } // To keep track of selected words
}

public class WordMapping
{
    public int Id { get; set; }
    public int ElberfelderWordId { get; set; }
    public int GreekWordId { get; set; }
}
