namespace UnshackledWord.Domain.WebApi.Vocab;

/// <summary>Body of <c>POST api/vocab/sessions</c> (roadmap 0022.8): start the study session of a course, or resume the open one.</summary>
public sealed class StartVocabSessionRequest
{
    /// <summary>Course code, e.g. <c>nt-greek</c>.</summary>
    public string Course { get; set; } = string.Empty;
}

/// <summary>Where the session stands. Words are the 20-word target (review and new words); a word is done after its three quizzes and once it left the open wave.</summary>
public sealed class VocabSessionProgress
{
    public int CardsServed { get; set; }
    public int WordsTotal { get; set; }
    public int WordsDone { get; set; }
    public int NewWordsTotal { get; set; }
    public int NewWordsIntroduced { get; set; }

    /// <summary>Words introduced and not yet passed. They must pass before the session ends.</summary>
    public int OpenWave { get; set; }
}

public sealed class StartVocabSessionResponse
{
    public int SessionId { get; set; }
    public string Course { get; set; } = string.Empty;

    /// <summary>True when an open session of this course was resumed instead of started.</summary>
    public bool Resumed { get; set; }

    /// <summary>True with no session (<c>sessionId</c> 0) when nothing is due and there is nothing new: the UI shows its "nothing due" state.</summary>
    public bool Finished { get; set; }

    public VocabSessionSummaryResponse? Summary { get; set; }

    public int ReviewWords { get; set; }
    public int NewWords { get; set; }
    public VocabSessionProgress Progress { get; set; } = new();

    /// <summary>Flagged cards of the user (revised or removed words, see <c>GET api/vocab/changes</c>).</summary>
    public int ChangedCount { get; set; }
}

/// <summary>The word a card is about. <c>Lang</c> is <c>grc</c> or <c>he</c> (script, font and direction).</summary>
public sealed class VocabCardEntryResponse
{
    public int Id { get; set; }
    public string Lemma { get; set; } = string.Empty;
    public string? DictionaryForm { get; set; }
    public string? Transliteration { get; set; }
    public string? Pos { get; set; }
    public string Lang { get; set; } = string.Empty;

    /// <summary>Presigned URL of the pronunciation; null without audio or without configured object storage.</summary>
    public string? AudioUrl { get; set; }
}

public sealed class VocabCardOptionResponse
{
    /// <summary>Position of the option (0-based). Opaque: it says nothing about which option is correct.</summary>
    public int Id { get; set; }

    public string Text { get; set; } = string.Empty;

    /// <summary>Reverse MC only: <c>grc</c> or <c>he</c>, the script of the lemma options (script, font and direction); omitted for German options.</summary>
    public string? Lang { get; set; }
}

/// <summary>One piece of the typing layout: a <c>group</c> (<c>length</c> boxes, never the letters) or a fixed <c>separator</c> (<c>text</c>).</summary>
public sealed class VocabTypingPartResponse
{
    /// <summary><c>group</c> or <c>separator</c>.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>Group only: number of base letters (accents and breathings do not count).</summary>
    public int? Length { get; set; }

    /// <summary>Separator only: the fixed text between groups, e.g. <c>, -</c>.</summary>
    public string? Text { get; set; }
}

/// <summary>The card to show. It never says which MC option is correct.</summary>
public sealed class VocabCardResponse
{
    /// <summary>Position on the session's timeline; <c>answers</c> must send it back.</summary>
    public int Seq { get; set; }

    /// <summary><c>introduction</c>, <c>multiple_choice</c>, <c>reverse_multiple_choice</c> or <c>typing</c>.</summary>
    public string Kind { get; set; } = string.Empty;

    public VocabCardEntryResponse Entry { get; set; } = new();

    /// <summary>Introduction only: the accepted meanings in rank order with counts.</summary>
    public List<VocabMeaningResponse>? Meanings { get; set; }

    /// <summary>Reverse multiple choice only: the German meaning to find the lemma for (the entry's top accepted meaning). <c>entry</c> is then not the answer to show; the UI shows only this prompt.</summary>
    public string? Prompt { get; set; }

    /// <summary>Multiple choice and reverse multiple choice: the options in display order (German for MC, lemmas with <c>lang</c> for reverse MC).</summary>
    public List<VocabCardOptionResponse>? Options { get; set; }

    /// <summary>Typing only: the German meaning(s) to type the lemma form for (top accepted meaning first). <c>entry</c> then carries only <c>lang</c> and <c>pos</c>.</summary>
    public List<string>? TypingPrompt { get; set; }

    /// <summary>Typing only: the groups and separators of the target form; <c>answers</c> must send one string per <c>group</c> part, in order.</summary>
    public List<VocabTypingPartResponse>? Layout { get; set; }

    /// <summary>Introduction only: one sample verse with the word highlighted (<c>isHit</c> marks the highlighted words of <c>original</c> and <c>german</c>); null without one.</summary>
    public VocabSampleVerseResponse? Verse { get; set; }
}

public sealed class VocabSessionWordResponse
{
    public int EntryId { get; set; }
    public string Lemma { get; set; } = string.Empty;
    public string? DictionaryForm { get; set; }
    public string Lang { get; set; } = string.Empty;
    public bool IsNew { get; set; }
    public int Reps { get; set; }
    public int Correct { get; set; }
    public double StrengthBefore { get; set; }
    public double StrengthAfter { get; set; }

    /// <summary><c>moved</c>, <c>held</c> or <c>dropped</c> (whole blocks of the 0–5 bar).</summary>
    public string Change { get; set; } = string.Empty;
}

/// <summary>Shown when the session is finished.</summary>
public sealed class VocabSessionSummaryResponse
{
    public int NewWords { get; set; }
    public int ReviewedWords { get; set; }
    public int Answers { get; set; }
    public int CorrectAnswers { get; set; }
    public List<VocabSessionWordResponse> Words { get; set; } = [];
}

/// <summary>Route of <c>GET api/vocab/sessions/{id}/next</c>.</summary>
public sealed class GetVocabSessionNextRequest
{
    public int Id { get; set; }
}

/// <summary>The next card, or <c>finished</c> with the summary (and no card). Repeated calls return the same card until it is answered.</summary>
public sealed class VocabNextResponse
{
    public int SessionId { get; set; }
    public bool Finished { get; set; }
    public VocabSessionProgress Progress { get; set; } = new();
    public VocabCardResponse? Card { get; set; }
    public VocabSessionSummaryResponse? Summary { get; set; }
}

/// <summary>Route and body of <c>POST api/vocab/sessions/{id}/answers</c>.</summary>
public sealed class SubmitVocabAnswerRequest
{
    public int Id { get; set; }

    /// <summary>The <c>seq</c> of the card answered. A stale seq is answered with 409.</summary>
    public int Seq { get; set; }

    /// <summary>The chosen option of a multiple-choice card; omitted for an introduction (the acknowledgment "verstanden").</summary>
    public int? OptionId { get; set; }

    /// <summary>Typing only: one string per letter group of the card's <c>layout</c>, in order (separators are not sent).</summary>
    public List<string>? Answers { get; set; }

    /// <summary>Time from showing the card to the answer, for the review log.</summary>
    public int? ResponseMs { get; set; }
}

/// <summary>The strength bar (0–5) before and after the answer. <c>Change</c> compares whole blocks: <c>moved</c> (up), <c>held</c>, <c>dropped</c>.</summary>
public sealed class VocabStrengthResponse
{
    public double Before { get; set; }
    public double After { get; set; }
    public string Change { get; set; } = string.Empty;
}

public sealed class VocabAnswerResponse
{
    public int Seq { get; set; }
    public string Kind { get; set; } = string.Empty;

    /// <summary>Judged on the server. An introduction is always true.</summary>
    public bool Correct { get; set; }

    /// <summary>The option that was correct (the entry's top meaning, or its lemma in reverse MC); null for an introduction.</summary>
    public int? CorrectOptionId { get; set; }

    /// <summary>Every accepted meaning of the entry (any of them is right); in reverse MC the entry's lemma form (dictionary form, else lemma).</summary>
    public List<string> CorrectAnswers { get; set; } = [];

    /// <summary>Typing only: per letter group of the layout, whether the typed text matched (accents, breathings and case ignored).</summary>
    public List<bool>? GroupResults { get; set; }

    /// <summary>The text of the option the learner picked; for typing the groups joined with the separators.</summary>
    public string? Given { get; set; }

    public VocabCardEntryResponse Entry { get; set; } = new();

    /// <summary>Null for an introduction (the card is only created).</summary>
    public VocabStrengthResponse? Strength { get; set; }

    /// <summary>When the card is due next.</summary>
    public DateTimeOffset? Due { get; set; }

    /// <summary>One sample verse with the word highlighted; null without one.</summary>
    public VocabSampleVerseResponse? Verse { get; set; }

    public bool Finished { get; set; }
    public VocabSessionProgress Progress { get; set; } = new();
    public VocabSessionSummaryResponse? Summary { get; set; }
}
