using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using UnshackledWord.Domain.Models.BibleStructure;
using UnshackledWord.Domain.Models.Dbo;
using UnshackledWord.Domain.Models.Grammar;
using UnshackledWord.Domain.WebApi.BibleTagger.GetVerse;
using UnshackledWord.Domain.WebApi.BibleTagger.SaveElbGrammar;

namespace UnshackledWord.Tooling.BibleTagger.Components.Pages;

public partial class ElbGrammar : ComponentBase
{
    private BibleReference bibleReference = new()
    {
        BookId = 1,
        Chapter = 1,
        Verse = 1
    };
    private List<BibleBookDbo> bibleBooks = [];
    private List<WordGrammarItem> elberfelderWords = [];
    private bool invalidSelection = false;
    private GetVerseForElbGrammarResponse verseResponse = new();
    private bool ShowNotification = false;
    private readonly Dictionary<PartOfSpeech, string> PartOfSpeechOptions = Enum.GetValues<PartOfSpeech>()
        .ToDictionary(pos => pos, pos => pos.ToString());

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
        bibleBooks = await MetaRepo.GetBibleBooksAsync(1);
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

        MappingResult = await ElbRepo.SaveVerseAsync(selectedElberfelderWords.Select(x => x.ElbWord).ToList());

        // Deselect all words after saving
        foreach (var word in elberfelderWords)
        {
            word.Selected = false;
        }
    }
}

public class WordGrammarItem
{
    public Elb1871WordGrammarDto ElbWord { get; set; } = default!;
    public bool Selected { get; set; } // To keep track of selected words
}
