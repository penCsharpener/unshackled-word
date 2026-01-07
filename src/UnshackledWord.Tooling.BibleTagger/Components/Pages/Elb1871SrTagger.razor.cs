using Microsoft.JSInterop;
using UnshackledWord.Domain.Models.BibleStructure;
using UnshackledWord.Domain.Models.Dbo;
using UnshackledWord.Domain.WebApi.BibleTagger.CreateElbSrMapping;
using UnshackledWord.Domain.WebApi.BibleTagger.GetVerse;

namespace UnshackledWord.Tooling.BibleTagger.Components.Pages;

public partial class Elb1871SrTagger
{
    private BibleReference bibleReference = new()
    {
        BookId = 40,
        Chapter = 1,
        Verse = 1
    };
    private List<BibleBookDbo> bibleBooks = [];
    private List<WordItem> elberfelderWords = [];
    private List<WordItem> greekWords = [];
    private bool invalidSelection = false;
    private GetVerseForElbTaggingResponse _verseForElbTaggingResponse = new();
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
        _verseForElbTaggingResponse = await ElbRepo.GetVerseAsync(bibleReference.BookId, bibleReference.Chapter, bibleReference.Verse);
        // Fetch words from the database for the selected book, chapter, and verse
        elberfelderWords = _verseForElbTaggingResponse.ElberfelderWords.Select(x => new WordItem()
        {
            Id = x.Id,
            PartOfSpeech = x.PartOfSpeech,
            Strongs = x.Strongs,
            Text = x.PlainWord!,
            Lemma = x.Lemma
        }).ToList();
        greekWords = _verseForElbTaggingResponse.SrWords.Select(x => new WordItem()
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

        MappingResult = await ElbRepo.CreateMappingAsync(_verseForElbTaggingResponse.ElberfelderWords.First(x => x.Id == elWord.Id),
            _verseForElbTaggingResponse.SrWords.First(x => x.Id == grWord.Id));

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

public class WordItem
{
    public int Id { get; set; }
    public string Text { get; set; } = default!;
    public string? Lemma { get; set; } = default!;
    public string? PartOfSpeech { get; set; } = default!;
    public string? Strongs { get; set; } = default!;
    public bool Selected { get; set; } // To keep track of selected words
}
