using UnshackledWord.Domain.WebApi.Elberfelder.GetWordDetail;

namespace UnshackledWord.Domain.WebApi.Maintainer;

// Roadmap 0028.7f: the LXX review page /edit/lxx (Admin/Maintainer). Chapters are Rahlfs chapters (LxxVerses.Chapter), as the AI proposals and
// the lxx-tag runs are keyed; a "unit" is one LxxVerses row (a verse, a subverse or an addition; Daniel has both text forms).

/// <summary>Open review work of one Rahlfs chapter.</summary>
public sealed class LxxReviewChapterCountResponse
{
    public int Chapter { get; set; }

    /// <summary>Units with an open proposal of either source: a <c>proposed</c> tag (nt-form prefill) or an open AI proposal.</summary>
    public int OpenUnits { get; set; }

    /// <summary>Units with an open AI proposal (<c>AgentVerseProposals</c>, language <c>lxx</c>).</summary>
    public int AiUnits { get; set; }

    /// <summary>Words of open AI proposals with confidence <c>low</c>.</summary>
    public int LowWords { get; set; }
}

public sealed class LxxReviewBookResponse
{
    public int BibleBookId { get; set; }
    public int OpenUnits { get; set; }
    public int AiUnits { get; set; }
    public int LowWords { get; set; }

    /// <summary>Only chapters with open work, ascending.</summary>
    public List<LxxReviewChapterCountResponse> Chapters { get; set; } = [];
}

/// <summary>Answer of <c>GET api/maintainer/lxx/review/books</c>: books (1..39) with open work, ascending.</summary>
public sealed class LxxReviewBooksResponse
{
    public List<LxxReviewBookResponse> Books { get; set; } = [];
}

public sealed class GetLxxReviewChapterRequest
{
    /// <summary>1..39.</summary>
    public int BibleBookId { get; set; }

    /// <summary>Rahlfs chapter.</summary>
    public int Chapter { get; set; }
}

/// <summary>A word tag: TBESG lemma, Strong's (<c>G0746</c>, <c>G20969</c>), Robinson code.</summary>
public sealed class LxxReviewTagResponse
{
    /// <summary>In TBESG spelling (an nt-form prefill's STEP lemma is rewritten like the AI package does), so sending it back passes validation.</summary>
    public string Lemma { get; set; } = string.Empty;

    public string Strongs { get; set; } = string.Empty;
    public string Morphology { get; set; } = string.Empty;

    /// <summary><c>proposed</c> or <c>confirmed</c>.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary><c>nt-form</c>, <c>ai</c> or <c>human</c>.</summary>
    public string Source { get; set; } = string.Empty;
}

/// <summary>The AI proposal for one word.</summary>
public sealed class LxxReviewAiWordResponse
{
    public string Lemma { get; set; } = string.Empty;
    public string Strongs { get; set; } = string.Empty;
    public string Morphology { get; set; } = string.Empty;

    /// <summary><c>high</c> or <c>low</c>.</summary>
    public string Confidence { get; set; } = "low";

    /// <summary>Equal to the prefill the model was shown.</summary>
    public bool Prefilled { get; set; }

    /// <summary>STEP Hebrew word ids the model linked.</summary>
    public List<int> HebrewWordIds { get; set; } = [];
}

public sealed class LxxReviewWordResponse
{
    public int Id { get; set; }
    public int Position { get; set; }
    public string Greek { get; set; } = string.Empty;

    /// <summary>The current tag (any status); absent when untagged.</summary>
    public LxxReviewTagResponse? Tag { get; set; }

    /// <summary>Stored <c>LxxHebrewLinks</c> (STEP Hebrew word ids), any status.</summary>
    public List<int> HebrewWordIds { get; set; } = [];

    /// <summary>True when every stored link is <c>confirmed</c> (false without links).</summary>
    public bool LinksConfirmed { get; set; }

    /// <summary>The open AI proposal's answer for this word; absent when there is none or the word was dropped (see the proposal flags).</summary>
    public LxxReviewAiWordResponse? Ai { get; set; }
}

/// <summary>A STEP Hebrew word of the unit's Hebrew verse, in reading order (<see cref="Number"/> 1..).</summary>
public sealed class LxxReviewHebrewWordResponse
{
    public int Id { get; set; }
    public int Number { get; set; }
    public string Text { get; set; } = string.Empty;
    public string? Strongs { get; set; }
    public string? Gloss { get; set; }
    public string? Morphology { get; set; }

    /// <summary>The Elb1871 words mapped to this Hebrew word, space separated; absent when none.</summary>
    /// <summary><see cref="Morphology"/> decoded into translation keys (root segment); absent without a code.</summary>
    public MorphologyResponse? MorphologyDetail { get; set; }

    public string? German { get; set; }

    /// <summary>Ids of the Elb1871 words mapped to this Hebrew word (see <see cref="LxxReviewUnitResponse.German"/>); absent when none.</summary>
    public List<int>? GermanWordIds { get; set; }

    /// <summary>The Strong's lexicon note as plain text (shortened); absent when there is none.</summary>
    public string? Note { get; set; }
}

public sealed class LxxReviewFlagResponse
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;

    /// <summary>Swete position of the word, when the flag is about one.</summary>
    public int? Position { get; set; }
}

/// <summary>The open AI proposal of a unit.</summary>
public sealed class LxxReviewProposalResponse
{
    public long Id { get; set; }
    public int RunId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string? Note { get; set; }

    /// <summary>Dropped or marked words (<c>no-strongs</c>, <c>morph-invalid</c>, ...).</summary>
    public List<LxxReviewFlagResponse> Flags { get; set; } = [];
}

public sealed class LxxReviewUnitResponse
{
    public int LxxVerseId { get; set; }
    public int Chapter { get; set; }
    public int Verse { get; set; }
    public int Subverse { get; set; }

    /// <summary>TVTMS style label as the reader endpoint gives it (<c>Rut.1:1</c>, <c>1Ki.2:35.1</c>).</summary>
    public string Ref { get; set; } = string.Empty;

    /// <summary><c>og</c>/<c>th</c> in Daniel, else empty.</summary>
    public string TextForm { get; set; } = string.Empty;

    public string MapKind { get; set; } = string.Empty;
    public bool IsAddition { get; set; }

    /// <summary>Elberfelder verse the unit renders; null for additions.</summary>
    public int? HebRefId { get; set; }

    /// <summary>A proposed tag or an open AI proposal waits for review.</summary>
    public bool Open { get; set; }

    /// <summary>At least one word has a confirmed tag ("KI neu" skips such units).</summary>
    public bool HasConfirmed { get; set; }

    public List<LxxReviewWordResponse> Words { get; set; } = [];
    public List<LxxReviewHebrewWordResponse> Hebrew { get; set; } = [];

    /// <summary>The Elb1871 verse of <see cref="HebRefId"/> word by word, for reference under the Hebrew line; empty for additions.</summary>
    public List<LxxReviewGermanWordResponse> German { get; set; } = [];

    public LxxReviewProposalResponse? Proposal { get; set; }
}

/// <summary>One Elb1871 word as printed (with punctuation).</summary>
public sealed class LxxReviewGermanWordResponse
{
    public int Id { get; set; }
    public string Text { get; set; } = string.Empty;
}

/// <summary>Answer of <c>GET api/maintainer/lxx/review/{bibleBookId}/{chapter}</c>: every unit of the Rahlfs chapter in Swete's reading order.</summary>
public sealed class LxxReviewChapterResponse
{
    public int BibleBookId { get; set; }
    public int Chapter { get; set; }
    public List<LxxReviewUnitResponse> Units { get; set; } = [];
}

/// <summary>The final state of one word as the maintainer accepted or edited it.</summary>
public sealed class LxxReviewWordInput
{
    public int LxxWordId { get; set; }

    /// <summary>Lemma, Strong's and code: all three or none. None leaves the stored tag as it is (a word without TBESG entry, links only).</summary>
    public string? Lemma { get; set; }

    public string? Strongs { get; set; }
    public string? Morphology { get; set; }

    /// <summary>STEP Hebrew word ids of the unit's Hebrew verse; replaces the word's links.</summary>
    public List<int> HebrewWordIds { get; set; } = [];
}

/// <summary>Body of <c>POST api/maintainer/lxx/review/confirm</c>.</summary>
public sealed class ConfirmLxxUnitRequest
{
    public int LxxVerseId { get; set; }

    /// <summary>The open AI proposal the page showed (null: none). Another open proposal on the server answers 409.</summary>
    public long? ProposalId { get; set; }

    public List<LxxReviewWordInput> Words { get; set; } = [];
}

public sealed class ConfirmLxxUnitResponse
{
    public int LxxVerseId { get; set; }

    /// <summary>Audit action written: <c>confirm</c>, <c>edit</c> (a confirmed tag or link changed), or empty when nothing changed (no audit row).</summary>
    public string Action { get; set; } = string.Empty;

    public int ConfirmedWords { get; set; }

    /// <summary><c>merged</c>, <c>edited-merged</c>, or absent without an AI proposal.</summary>
    public string? ProposalStatus { get; set; }
}

/// <summary>Body of <c>POST api/maintainer/lxx/review/discard</c>.</summary>
public sealed class DiscardLxxUnitRequest
{
    public int LxxVerseId { get; set; }

    /// <summary>The open AI proposal the page showed; another (or none) answers 409.</summary>
    public long ProposalId { get; set; }
}

public sealed class DiscardLxxUnitResponse
{
    public int LxxVerseId { get; set; }
    public long ProposalId { get; set; }
    public string ProposalStatus { get; set; } = "discarded";
}

/// <summary>The words of one unit the maintainer saw on screen.</summary>
public sealed class LxxSafeUnitInput
{
    public int LxxVerseId { get; set; }

    /// <summary>The open AI proposal shown for the unit; a unit whose proposal changed meanwhile is skipped (stale).</summary>
    public long ProposalId { get; set; }

    public List<int> LxxWordIds { get; set; } = [];
}

/// <summary>Body of <c>POST api/maintainer/lxx/review/confirm-chapter-safe</c>.</summary>
public sealed class ConfirmLxxSafeRequest
{
    public int BibleBookId { get; set; }
    public int Chapter { get; set; }
    public List<LxxSafeUnitInput> Units { get; set; } = [];
}

public sealed class ConfirmLxxSafeResponse
{
    /// <summary>Units with at least one word confirmed (one audit row each).</summary>
    public int Units { get; set; }

    public int ConfirmedWords { get; set; }

    /// <summary>Listed words whose AI confidence is <c>low</c>: left open.</summary>
    public int SkippedLow { get; set; }

    /// <summary>Listed words without an AI answer, already confirmed, or not of the unit.</summary>
    public int SkippedOther { get; set; }

    /// <summary>Units skipped because their open proposal is another one than the page showed (or gone).</summary>
    public List<int> StaleUnits { get; set; } = [];

    /// <summary>AI proposals closed as <c>merged</c> because every word of theirs is now confirmed as proposed.</summary>
    public int MergedProposals { get; set; }
}

/// <summary>Query of <c>GET api/maintainer/lxx/lexicon</c>.</summary>
public sealed class SearchLxxLexiconRequest
{
    /// <summary>A Strong's number (<c>G746</c>, <c>g0746</c>) or the start of a lemma (accents optional).</summary>
    public string Q { get; set; } = string.Empty;

    /// <summary>1..50, default 20.</summary>
    public int? Limit { get; set; }
}

public sealed class LxxLexiconEntryResponse
{
    public string Strongs { get; set; } = string.Empty;
    public string Lemma { get; set; } = string.Empty;
    public string Gloss { get; set; } = string.Empty;
}

public sealed class SearchLxxLexiconResponse
{
    public List<LxxLexiconEntryResponse> Entries { get; set; } = [];
}

/// <summary>Query of <c>GET api/maintainer/lxx/morphology</c> (a query parameter, because crasis codes contain spaces and <c>+</c>).</summary>
public sealed class CheckLxxMorphologyRequest
{
    public string Code { get; set; } = string.Empty;
}

public sealed class CheckLxxMorphologyResponse
{
    /// <summary>Upper case, trimmed: what confirm would store.</summary>
    public string Code { get; set; } = string.Empty;

    public bool Valid { get; set; }

    /// <summary>Why the code is refused (English, from <c>GreekMorphologyCode</c>).</summary>
    public string? Error { get; set; }

    public string? German { get; set; }
    public string? English { get; set; }
}
