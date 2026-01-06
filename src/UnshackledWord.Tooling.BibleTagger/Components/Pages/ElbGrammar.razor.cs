using Microsoft.JSInterop;
using UnshackledWord.Domain.Models.Dbo;
using UnshackledWord.Domain.WebApi.BibleTagger.GetVerse;
using UnshackledWord.Domain.WebApi.BibleTagger.SaveElbGrammar;

namespace UnshackledWord.Tooling.BibleTagger.Components.Pages;

public partial class ElbGrammar
{
    private BibleReference bibleReference = new BibleReference()
    {
        BookId = 40,
        Chapter = 1,
        Verse = 1
    };
    private List<BibleBookDbo> bibleBooks = [];
    private List<WordGrammarItem> elberfelderWords = [];
    private bool invalidSelection = false;
    private GetVerseForElbGrammarResponse verseResponse = new();
    private bool ShowNotification = false;

    public SaveElbGrammarResponse? MappingResult
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
        elberfelderWords = verseResponse.ElberfelderWords.Select(x => new WordGrammarItem
        {
            ElbWord = x,
            Selected = false
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

    private void ToggleSelection(WordGrammarItem word)
    {
        word.Selected = !word.Selected;
    }

    private async Task SaveWordMappingAsync()
    {
        var selectedElberfelderWords = elberfelderWords.Where(w => w.Selected).ToList();

        invalidSelection = selectedElberfelderWords.Count == 0;

        if (invalidSelection)
        {
            return;
        }

        var elWord = selectedElberfelderWords.First();

        MappingResult = await ElbRepo.SaveVerseAsync(verseResponse.ElberfelderWords);

        // Deselect all words after saving
        foreach (var word in elberfelderWords)
        {
            word.Selected = false;
        }
    }
}

public class WordGrammarItem
{
    public Elb1871WordDbo ElbWord { get; set; } = default!;
    public bool Selected { get; set; } // To keep track of selected words
}
