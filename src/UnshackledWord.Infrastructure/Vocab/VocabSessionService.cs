using System.Data;
using System.Text.Json;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Features.Vocab;
using UnshackledWord.Application.Features.Vocab.Session;
using UnshackledWord.Domain.Models.Vocab;
using UnshackledWord.Domain.WebApi.Vocab;

namespace UnshackledWord.Infrastructure.Vocab;

public enum VocabSessionStatus
{
    Ok,
    NotFound,
    Conflict,
    Invalid,
}

public sealed record VocabSessionOutcome<T>(VocabSessionStatus Status, T? Response, string? Message = null)
    where T : class
{
    public static VocabSessionOutcome<T> Ok(T response) => new(VocabSessionStatus.Ok, response);

    public static VocabSessionOutcome<T> Fail(VocabSessionStatus status, string message) => new(status, null, message);
}

/// <summary>
/// The study session engine, part 1 (roadmap 0022.8): start or resume, serve the next card, judge an answer. Quiz types: <c>introduction</c>,
/// <c>multiple_choice</c> and (0022.9) <c>reverse_multiple_choice</c>, chosen by <see cref="VocabQuizTypes"/>. The engine state lives in <c>VocabSessions.Plan</c>; the pure rules are in <see cref="VocabSessionPlanner"/> and
/// <see cref="VocabDistractorPicker"/>. FSRS runs on every answer (Lexio: repeats within a session count; wrong = Again, correct = Good).
/// </summary>
public sealed class VocabSessionService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IDbConnectionFactory _connectionFactory;
    private readonly VocabScheduler _scheduler;
    private readonly TimeProvider _time;
    private readonly VocabEntryRepository? _entries;
    private readonly IObjectStorage? _storage;
    private readonly Random _random;

    /// <summary><paramref name="entries"/> supplies the sample verse; tests pass null for none. <paramref name="random"/> is for tests, null = shared.</summary>
    public VocabSessionService(IDbConnectionFactory connectionFactory, VocabScheduler scheduler, TimeProvider time, VocabEntryRepository? entries, Random? random = null, IObjectStorage? storage = null)
    {
        _connectionFactory = connectionFactory;
        _scheduler = scheduler;
        _time = time;
        _entries = entries;
        _storage = storage;
        _random = random ?? Random.Shared;
    }

    public Task<VocabSessionOutcome<StartVocabSessionResponse>> StartAsync(int userId, string course, CancellationToken token) =>
        RunAsync((c, t) => StartAsync(c, t, userId, course), token);

    public Task<VocabSessionOutcome<VocabNextResponse>> NextAsync(int userId, int sessionId, CancellationToken token) =>
        RunAsync((c, t) => NextAsync(c, t, userId, sessionId), token);

    public Task<VocabSessionOutcome<VocabAnswerResponse>> AnswerAsync(int userId, int sessionId, int seq, int? optionId, int? responseMs, CancellationToken token, IReadOnlyList<string>? answers = null) =>
        RunAsync((c, t) => AnswerAsync(c, t, userId, sessionId, seq, optionId, responseMs, answers), token);

    private async Task<VocabSessionOutcome<T>> RunAsync<T>(Func<IDbConnection, IDbTransaction, Task<VocabSessionOutcome<T>>> operation, CancellationToken token)
        where T : class
    {
        using var connection = await _connectionFactory.CreateDbConnectionAsync(token);
        using var transaction = connection.BeginTransaction();
        var outcome = await operation(connection, transaction);

        if (outcome.Status == VocabSessionStatus.Ok)
        {
            transaction.Commit();
        }
        else
        {
            transaction.Rollback();
        }

        return outcome;
    }

    // ---- start ---------------------------------------------------------------------------------------------------------------------

    public async Task<VocabSessionOutcome<StartVocabSessionResponse>> StartAsync(IDbConnection c, IDbTransaction t, int userId, string courseCode)
    {
        var outcome = await StartCoreAsync(c, t, userId, courseCode);

        if (outcome.Response is not null)
        {
            outcome.Response.ChangedCount = await VocabChangeService.CountAsync(c, t, userId);
        }

        return outcome;
    }

    private async Task<VocabSessionOutcome<StartVocabSessionResponse>> StartCoreAsync(IDbConnection c, IDbTransaction t, int userId, string courseCode)
    {
        var course = await VocabSessionRepository.GetCourseAsync(c, t, courseCode);

        if (course is null)
        {
            return VocabSessionOutcome<StartVocabSessionResponse>.Fail(VocabSessionStatus.NotFound, "Unknown course.");
        }

        var open = await VocabSessionRepository.GetOpenSessionAsync(c, t, userId, course.Id);

        if (open is not null)
        {
            var existing = Read(open);

            return VocabSessionOutcome<StartVocabSessionResponse>.Ok(StartResponse(open.Id, existing, resumed: true));
        }

        var now = _time.GetUtcNow();
        var newIds = await VocabSessionRepository.GetNewCandidatesAsync(c, t, userId, course.Track, VocabSessionRules.MaxNewWords);
        var due = (await VocabSessionRepository.GetDueCardsAsync(c, t, userId, course.Track, now.UtcDateTime))
            .Select(row => (Row: row, Card: ToCard(row)))
            .Select(x => (x.Row, x.Card, Retrievability: _scheduler.Retrievability(x.Card, now)))
            .OrderBy(x => x.Retrievability)
            .ThenBy(x => x.Row.Due)
            .ToList();
        var (reviewCount, newCount) = VocabSessionRules.Size(due.Count, newIds.Count);

        if (reviewCount + newCount == 0)
        {
            return VocabSessionOutcome<StartVocabSessionResponse>.Ok(new StartVocabSessionResponse { Course = course.Code, Finished = true, Summary = new VocabSessionSummaryResponse() });
        }

        var plan = new VocabSessionPlan { CourseCode = course.Code, Track = course.Track };
        plan.Words.AddRange(due.Take(reviewCount).Select(x => new VocabPlanWord
        {
            EntryId = x.Row.EntryId,
            StrengthStart = VocabScheduler.Strength(x.Card),
            StrengthNow = VocabScheduler.Strength(x.Card),
        }));
        plan.Words.AddRange(newIds.Take(newCount).Select(id => new VocabPlanWord { EntryId = id, IsNew = true }));

        var id = await VocabSessionRepository.InsertSessionAsync(c, t, userId, course.Id, now.UtcDateTime, JsonSerializer.Serialize(plan, Json));

        if (id is null)
        {
            // a concurrent start (double click) opened the session first: resume that one
            var raced = await VocabSessionRepository.GetOpenSessionAsync(c, t, userId, course.Id);

            return raced is null
                ? VocabSessionOutcome<StartVocabSessionResponse>.Fail(VocabSessionStatus.Conflict, "A session was started at the same time. Try again.")
                : VocabSessionOutcome<StartVocabSessionResponse>.Ok(StartResponse(raced.Id, Read(raced), resumed: true));
        }

        return VocabSessionOutcome<StartVocabSessionResponse>.Ok(StartResponse(id.Value, plan, resumed: false));
    }

    private static StartVocabSessionResponse StartResponse(int id, VocabSessionPlan plan, bool resumed) => new()
    {
        SessionId = id,
        Course = plan.CourseCode,
        Resumed = resumed,
        ReviewWords = plan.Words.Count(w => !w.IsNew && !w.Dropped),
        NewWords = plan.Words.Count(w => w.IsNew && !w.Dropped),
        Progress = Progress(plan),
    };

    // ---- next ----------------------------------------------------------------------------------------------------------------------

    public async Task<VocabSessionOutcome<VocabNextResponse>> NextAsync(IDbConnection c, IDbTransaction t, int userId, int sessionId)
    {
        var session = await VocabSessionRepository.GetSessionAsync(c, t, userId, sessionId);

        if (session is null)
        {
            return VocabSessionOutcome<VocabNextResponse>.Fail(VocabSessionStatus.NotFound, "Unknown session.");
        }

        var plan = Read(session);

        if (session.FinishedAt is not null)
        {
            return VocabSessionOutcome<VocabNextResponse>.Ok(await FinishedNextAsync(c, t, sessionId, plan));
        }

        var now = _time.GetUtcNow();
        var disabled = (await VocabSessionRepository.GetDisabledQuizTypesAsync(c, t, userId)).ToHashSet();

        // the open card's entry was merged away, deleted or retired since it was served: drop it, or the session could never move on
        if (plan.Pending is { } open)
        {
            var openEntry = (await VocabSessionRepository.GetEntriesAsync(c, t, [open.EntryId])).GetValueOrDefault(open.EntryId);

            if (openEntry is null || openEntry.Retired || openEntry.Track != plan.Track)
            {
                VocabSessionPlanner.Drop(plan, open.EntryId);
                plan.Pending = null;
            }
        }

        while (plan.Pending is null)
        {
            var step = VocabSessionPlanner.Next(plan);

            if (step.Kind == VocabSessionStepKind.Done)
            {
                await VocabSessionRepository.SaveSessionAsync(c, t, sessionId, JsonSerializer.Serialize(plan, Json), now.UtcDateTime);

                return VocabSessionOutcome<VocabNextResponse>.Ok(await FinishedNextAsync(c, t, sessionId, plan));
            }

            var entry = (await VocabSessionRepository.GetEntriesAsync(c, t, [step.EntryId])).GetValueOrDefault(step.EntryId);
            var meanings = (await VocabSessionRepository.GetAcceptedMeaningsAsync(c, t, [step.EntryId]))[step.EntryId].ToList();

            // retired, moved to another track or without an accepted meaning since the session started: not quizzed
            if (entry is null || entry.Retired || entry.Track != plan.Track || meanings.Count == 0)
            {
                VocabSessionPlanner.Drop(plan, step.EntryId);

                continue;
            }

            if (step.Kind == VocabSessionStepKind.Introduce)
            {
                await VocabSessionRepository.EnsureCardAsync(c, t, userId, entry.Id, entry.ContentVersion, now.UtcDateTime);
                VocabSessionPlanner.Serve(plan, step, "introduction");

                continue;
            }

            // a review word whose card is gone (reset in between) comes back as a fresh card
            await VocabSessionRepository.EnsureCardAsync(c, t, userId, entry.Id, entry.ContentVersion, now.UtcDateTime);
            var createdAt = await VocabSessionRepository.GetCardCreatedAtAsync(c, t, userId, entry.Id) ?? now.UtcDateTime;
            var quiz = await BuildQuizAsync(c, t, plan, entry, meanings, VocabQuizTypes.AgeDays(createdAt, now.UtcDateTime), disabled);

            if (quiz is null)
            {
                VocabSessionPlanner.Drop(plan, step.EntryId);

                continue;
            }

            var served = VocabSessionPlanner.Serve(plan, step, quiz.Value.Kind, quiz.Value.Options);
            served.VerseRefId = quiz.Value.VerseRefId;
        }

        await VocabSessionRepository.SaveSessionAsync(c, t, sessionId, JsonSerializer.Serialize(plan, Json), null);

        return VocabSessionOutcome<VocabNextResponse>.Ok(new VocabNextResponse
        {
            SessionId = sessionId,
            Progress = Progress(plan),
            Card = await BuildCardAsync(c, t, plan.Pending),
        });
    }

    /// <summary>
    /// Picks the quiz type of a question: the candidates of <see cref="VocabQuizTypes.Candidates"/> (age ladder, learner's switches, fallback) in
    /// order, the first one that can be built wins. Later rows add their builders to the switch. Null when no type can be built.
    /// </summary>
    private async Task<(string Kind, List<VocabPendingOption> Options, int? VerseRefId)?> BuildQuizAsync(
        IDbConnection c, IDbTransaction t, VocabSessionPlan plan, VocabSessionRepository.EntryRow entry, List<VocabSessionRepository.MeaningRow> meanings, int ageDays, IReadOnlySet<string> disabled)
    {
        var candidates = VocabQuizTypes.Candidates(ageDays, disabled, _random);

        if (candidates.Count == 0)
        {
            candidates.Add(VocabQuizTypes.MultipleChoice);
        }

        // "None of the above" (Lexio): a fixed last option of the two multiple-choice types; for one card in five it is the right answer
        // and the four others are all distractors. The roll is stored with the options (no option of the entry), so reloads agree.
        var noneCorrect = _random.Next(100) < VocabSessionRules.NoneOfTheAbovePercent;
        foreach (var type in candidates)
        {
            int? verseRefId = null;
            List<VocabPendingOption>? options;

            switch (type)
            {
                case VocabQuizTypes.ReverseMultipleChoice:
                    options = WithNone(await BuildReverseOptionsAsync(c, t, plan, entry, meanings, noneCorrect));
                    break;
                case VocabQuizTypes.Listening:
                    // the audio is the prompt: no audio (or no storage to sign a URL) means no listening question
                    options = VocabAudio.UrlFor(_storage, entry.AudioObjectKey) is null ? null : await BuildReverseOptionsAsync(c, t, plan, entry, meanings);
                    break;
                case VocabQuizTypes.VerseContext:
                    var quizVerse = _entries is null ? null : await _entries.GetQuizVerseAsync(entry.Id, meanings.Select(m => m.German).ToList());
                    options = quizVerse is null ? null : await BuildOptionsAsync(c, t, plan, entry, meanings, quizVerse.MeaningIndex);
                    verseRefId = quizVerse?.RefId;
                    break;
                case VocabQuizTypes.Typing:
                    options = VocabText.Segment(FormOf(entry)).Any(x => x.IsGroup) ? [] : null;
                    break;
                default:
                    options = WithNone(await BuildOptionsAsync(c, t, plan, entry, meanings, 0, noneCorrect));
                    break;
            }

            if (options is not null)
            {
                return (type, options, verseRefId);
            }
        }

        return null;
    }

    /// <summary>Reverse MC: the correct option is the entry's lemma form; three distractor entries whose accepted meanings do not include the prompt meaning.</summary>
    private async Task<List<VocabPendingOption>?> BuildReverseOptionsAsync(IDbConnection c, IDbTransaction t, VocabSessionPlan plan, VocabSessionRepository.EntryRow entry, List<VocabSessionRepository.MeaningRow> meanings, bool noneCorrect = false)
    {
        var waveIds = plan.Words.Where(w => w.InWave && !w.Dropped && w.EntryId != entry.Id).Select(w => w.EntryId).ToList();
        var pool = await VocabSessionRepository.GetDistractorPoolAsync(c, t, plan.Track, entry.Id, entry.Pos, entry.Rank ?? 0, waveIds);
        var prompt = new VocabDistractorPrompt(entry.Id, entry.Pos, entry.Rank ?? 0, meanings[0].German, meanings.Select(m => m.German).ToList());
        var form = FormOf(entry);
        var distractors = VocabDistractorPicker.PickLemmas(prompt, form, pool, waveIds.ToHashSet(), noneCorrect ? VocabSessionRules.OptionCount : VocabSessionRules.OptionCount - 1, _random);

        if (distractors.Count == 0)
        {
            return null;
        }

        return distractors.Select(d => new VocabPendingOption { EntryId = d.EntryId, Text = d.Form ?? d.TopMeaning })
            .Concat(noneCorrect ? [] : [new VocabPendingOption { EntryId = entry.Id, Text = form }])
            .OrderBy(_ => _random.Next())
            .ToList();
    }

    /// <summary>Appends the fixed last "None of the above" option (the builders leave out the entry's own option when it is the right answer).</summary>
    private static List<VocabPendingOption>? WithNone(List<VocabPendingOption>? options)
    {
        options?.Add(new VocabPendingOption { EntryId = 0, Text = VocabSessionRules.NoneOfTheAboveText, IsNone = true });

        return options;
    }

    private static string FormOf(VocabSessionRepository.EntryRow entry) => entry.DictionaryForm ?? entry.Lemma;

    /// <summary>The correct option is the entry's top accepted meaning (verse context: the meaning the verse's rendering matches); three distractors by tier; shuffled. Null when no distractor exists at all.</summary>
    private async Task<List<VocabPendingOption>?> BuildOptionsAsync(IDbConnection c, IDbTransaction t, VocabSessionPlan plan, VocabSessionRepository.EntryRow entry, List<VocabSessionRepository.MeaningRow> meanings, int meaningIndex = 0, bool noneCorrect = false)
    {
        var waveIds = plan.Words.Where(w => w.InWave && !w.Dropped && w.EntryId != entry.Id).Select(w => w.EntryId).ToList();
        var pool = await VocabSessionRepository.GetDistractorPoolAsync(c, t, plan.Track, entry.Id, entry.Pos, entry.Rank ?? 0, waveIds);
        var prompt = new VocabDistractorPrompt(entry.Id, entry.Pos, entry.Rank ?? 0, meanings[meaningIndex].German, meanings.Select(m => m.German).ToList());
        var distractors = VocabDistractorPicker.Pick(prompt, pool, waveIds.ToHashSet(), noneCorrect ? VocabSessionRules.OptionCount : VocabSessionRules.OptionCount - 1, _random);

        if (distractors.Count == 0)
        {
            return null;
        }

        var options = distractors.Select(d => new VocabPendingOption { EntryId = d.EntryId, Text = d.TopMeaning })
            .Concat(noneCorrect ? [] : [new VocabPendingOption { EntryId = entry.Id, Text = meanings[meaningIndex].German }])
            .OrderBy(_ => _random.Next())
            .ToList();

        return options;
    }

    private async Task<VocabCardResponse> BuildCardAsync(IDbConnection c, IDbTransaction t, VocabPendingCard pending)
    {
        var entry = (await VocabSessionRepository.GetEntriesAsync(c, t, [pending.EntryId]))[pending.EntryId];
        var reverse = pending.Kind == VocabQuizTypes.ReverseMultipleChoice;
        var typing = pending.Kind == VocabQuizTypes.Typing;
        var listening = pending.Kind == VocabQuizTypes.Listening;
        var verseContext = pending.Kind == VocabQuizTypes.VerseContext;
        var card = new VocabCardResponse
        {
            Seq = pending.Seq,
            Kind = pending.Kind,
            Entry = listening ? ToListeningEntry(entry) : reverse || typing ? ToHiddenEntry(entry) : ToEntry(entry)
        };

        if (pending.Kind == "introduction")
        {
            card.Meanings = (await VocabSessionRepository.GetAcceptedMeaningsAsync(c, t, [pending.EntryId]))[pending.EntryId]
                .Select(m => new VocabMeaningResponse { German = m.German, Rank = m.Rank, Count = m.Count, Share = m.Share })
                .ToList();
            card.Verse = await VerseAsync(pending.EntryId);
        }
        else if (typing)
        {
            card.TypingPrompt = (await VocabSessionRepository.GetAcceptedMeaningsAsync(c, t, [pending.EntryId]))[pending.EntryId].Take(3).Select(m => m.German).ToList();
            card.Layout = VocabText.Segment(FormOf(entry)).Select(x => x.IsGroup
                ? new VocabTypingPartResponse { Kind = "group", Length = x.Length }
                : new VocabTypingPartResponse { Kind = "separator", Text = x.Text }).ToList();
        }
        else if (listening)
        {
            card.Options = pending.Options.Select((o, index) => new VocabCardOptionResponse { Id = index, Text = o.Text, Lang = Lang(entry.Track) }).ToList();
        }
        else if (verseContext)
        {
            card.Options = pending.Options.Select((o, index) => new VocabCardOptionResponse { Id = index, Text = o.Text }).ToList();
            card.Verse = await QuizVerseAsync(c, t, pending, withGaps: true);
        }
        else if (reverse)
        {
            card.Prompt = (await VocabSessionRepository.GetAcceptedMeaningsAsync(c, t, [pending.EntryId]))[pending.EntryId].First().German;
            card.Options = pending.Options.Select((o, index) => new VocabCardOptionResponse { Id = index, Text = o.Text, Lang = o.IsNone ? null : Lang(entry.Track), None = o.IsNone ? true : null }).ToList();
        }
        else
        {
            card.Options = pending.Options.Select((o, index) => new VocabCardOptionResponse { Id = index, Text = o.Text, None = o.IsNone ? true : null }).ToList();
        }

        return card;
    }

    private async Task<VocabNextResponse> FinishedNextAsync(IDbConnection c, IDbTransaction t, int sessionId, VocabSessionPlan plan) => new()
    {
        SessionId = sessionId,
        Finished = true,
        Progress = Progress(plan),
        Summary = await SummaryAsync(c, t, plan),
    };

    // ---- answers -------------------------------------------------------------------------------------------------------------------

    public async Task<VocabSessionOutcome<VocabAnswerResponse>> AnswerAsync(IDbConnection c, IDbTransaction t, int userId, int sessionId, int seq, int? optionId, int? responseMs, IReadOnlyList<string>? answers = null)
    {
        var session = await VocabSessionRepository.GetSessionAsync(c, t, userId, sessionId);

        if (session is null)
        {
            return VocabSessionOutcome<VocabAnswerResponse>.Fail(VocabSessionStatus.NotFound, "Unknown session.");
        }

        var plan = Read(session);
        var pending = plan.Pending;

        if (session.FinishedAt is not null || pending is null || pending.Seq != seq)
        {
            return VocabSessionOutcome<VocabAnswerResponse>.Fail(VocabSessionStatus.Conflict, "That card is no longer the open one. Ask for the next card.");
        }

        var now = _time.GetUtcNow();
        var entry = (await VocabSessionRepository.GetEntriesAsync(c, t, [pending.EntryId])).GetValueOrDefault(pending.EntryId);

        if (entry is null)
        {
            // merged away or deleted since the card was served; the next "next" call drops it from the plan
            return VocabSessionOutcome<VocabAnswerResponse>.Fail(VocabSessionStatus.Conflict, "The word of this card no longer exists. Ask for the next card.");
        }
        var accepted = pending.Kind is VocabQuizTypes.ReverseMultipleChoice or VocabQuizTypes.Listening or VocabQuizTypes.Typing
            ? new List<string> { FormOf(entry) }
            : (await VocabSessionRepository.GetAcceptedMeaningsAsync(c, t, [pending.EntryId]))[pending.EntryId].Select(m => m.German).ToList();
        var response = new VocabAnswerResponse { Seq = seq, Kind = pending.Kind, Entry = ToEntry(entry), CorrectAnswers = accepted };
        var dueRow = await VocabSessionRepository.GetCardForUpdateAsync(c, t, userId, pending.EntryId);
        var before = dueRow is null ? _scheduler.NewCard(now) : ToCard(dueRow);

        if (pending.Kind == "introduction")
        {
            response.Correct = true;
            await VocabSessionRepository.InsertReviewLogAsync(c, t, new VocabSessionRepository.ReviewLogEntry(
                userId, pending.EntryId, sessionId, "introduction", true, null, responseMs, null, null, null, null, null, null, now.UtcDateTime));
            plan.Pending = null;
            response.Due = before.Due;
        }
        else
        {
            int? correctIndex = null;
            string givenText;
            bool correct;

            if (pending.Kind == VocabQuizTypes.Typing)
            {
                var segments = VocabText.Segment(FormOf(entry));
                var groups = segments.Where(x => x.IsGroup).ToList();

                if (answers is null || answers.Count != groups.Count)
                {
                    return VocabSessionOutcome<VocabAnswerResponse>.Fail(VocabSessionStatus.Invalid, $"answers must hold one string per letter group ({groups.Count}).");
                }

                response.GroupResults = groups.Select((g, i) => VocabText.Normalize(answers[i]) == VocabText.Normalize(g.Text)).ToList();
                correct = response.GroupResults.All(x => x);
                var next = 0;
                givenText = string.Concat(segments.Select(x => x.IsGroup ? answers[next++].Trim() : x.Text));
            }
            else
            {
                if (optionId is null || optionId < 0 || optionId >= pending.Options.Count)
                {
                    return VocabSessionOutcome<VocabAnswerResponse>.Fail(VocabSessionStatus.Invalid, "optionId is not an option of this card.");
                }

                // no option of the entry itself: "None of the above" is the right answer
                correctIndex = pending.Options.FindIndex(o => !o.IsNone && o.EntryId == pending.EntryId);

                if (correctIndex < 0)
                {
                    correctIndex = pending.Options.FindIndex(o => o.IsNone);
                }

                givenText = pending.Options[optionId.Value].Text;
                correct = optionId.Value == correctIndex;
            }

            var result = _scheduler.Review(before, correct, now);

            if (dueRow is null)
            {
                await VocabSessionRepository.EnsureCardAsync(c, t, userId, pending.EntryId, entry.ContentVersion, now.UtcDateTime);
            }

            await VocabSessionRepository.UpdateCardAsync(c, t, userId, pending.EntryId, result.Card);
            await VocabSessionRepository.InsertReviewLogAsync(c, t, new VocabSessionRepository.ReviewLogEntry(
                userId, pending.EntryId, sessionId, pending.Kind, correct, Truncate(givenText, 120), responseMs, correct ? 3 : 1,
                result.StateBefore.ToString().ToLowerInvariant(), result.StabilityBefore, result.DifficultyBefore, result.StabilityAfter, result.DifficultyAfter, now.UtcDateTime));

            var strengthBefore = VocabScheduler.Strength(before);
            var strengthAfter = VocabScheduler.Strength(result.Card);
            VocabSessionPlanner.Answered(plan, pending.EntryId, correct, result.Card.State, strengthAfter);

            response.Correct = correct;
            response.CorrectOptionId = correctIndex;
            response.Given = givenText;
            response.Due = result.Card.Due;
            response.Strength = new VocabStrengthResponse { Before = strengthBefore, After = strengthAfter, Change = Change(strengthBefore, strengthAfter) };
        }

        response.Verse = pending.Kind == VocabQuizTypes.VerseContext ? await QuizVerseAsync(c, t, pending, withGaps: false) : await VerseAsync(pending.EntryId);

        // the answer closes the session when nothing is left (all quizzed, open wave passed, no word left to introduce)
        var finished = VocabSessionPlanner.Next(plan).Kind == VocabSessionStepKind.Done;
        await VocabSessionRepository.SaveSessionAsync(c, t, sessionId, JsonSerializer.Serialize(plan, Json), finished ? now.UtcDateTime : null);

        response.Finished = finished;
        response.Progress = Progress(plan);

        if (finished)
        {
            response.Summary = await SummaryAsync(c, t, plan);
        }

        return VocabSessionOutcome<VocabAnswerResponse>.Ok(response);
    }

    // ---- helpers -------------------------------------------------------------------------------------------------------------------

    /// <summary>Whole blocks of the 0–5 bar: up = moved, same = held, down = dropped.</summary>
    public static string Change(double before, double after)
    {
        var from = (int)Math.Floor(before);
        var to = (int)Math.Floor(after);

        return to > from ? "moved" : to < from ? "dropped" : "held";
    }

    public static VocabSessionProgress Progress(VocabSessionPlan plan)
    {
        var words = plan.Words.Where(w => !w.Dropped).ToList();

        // Fine-grained steps for the bar: an introduction (once answered) and up to RepsPerWord quizzes per word.
        // A word still in the open wave never reaches its last step, so the bar only fills with the word's pass.
        var introPending = plan.Pending is { Kind: "introduction" } pending ? pending.EntryId : (int?)null;
        var stepsTotal = words.Sum(w => VocabSessionRules.RepsPerWord + (w.IsNew ? 1 : 0));
        var stepsDone = words.Sum(w =>
            (w.IsNew && w.Introduced && w.EntryId != introPending ? 1 : 0)
            + Math.Min(w.Reps, w.InWave ? VocabSessionRules.RepsPerWord - 1 : VocabSessionRules.RepsPerWord));

        return new VocabSessionProgress
        {
            CardsServed = plan.Position,
            WordsTotal = words.Count,
            StepsDone = stepsDone,
            StepsTotal = stepsTotal,
            WordsDone = words.Count(w => (!w.IsNew || w.Introduced) && w.Reps >= VocabSessionRules.RepsPerWord && !w.InWave),
            NewWordsTotal = words.Count(w => w.IsNew),
            NewWordsIntroduced = words.Count(w => w.Introduced),
            OpenWave = words.Count(w => w.InWave),
        };
    }

    private async Task<VocabSessionSummaryResponse> SummaryAsync(IDbConnection c, IDbTransaction t, VocabSessionPlan plan)
    {
        var shown = plan.Words.Where(w => !w.Dropped && (w.Introduced || w.Reps > 0)).ToList();
        var entries = await VocabSessionRepository.GetEntriesAsync(c, t, shown.Select(w => w.EntryId));

        return new VocabSessionSummaryResponse
        {
            NewWords = shown.Count(w => w.IsNew),
            ReviewedWords = shown.Count(w => !w.IsNew),
            Answers = shown.Sum(w => w.Reps),
            CorrectAnswers = shown.Sum(w => w.Correct),
            Words = shown.Where(w => entries.ContainsKey(w.EntryId)).Select(w => new VocabSessionWordResponse
            {
                EntryId = w.EntryId,
                Lemma = entries[w.EntryId].Lemma,
                DictionaryForm = entries[w.EntryId].DictionaryForm,
                Lang = Lang(entries[w.EntryId].Track),
                IsNew = w.IsNew,
                Reps = w.Reps,
                Correct = w.Correct,
                StrengthBefore = w.StrengthStart,
                StrengthAfter = w.StrengthNow,
                Change = Change(w.StrengthStart, w.StrengthNow),
            }).ToList(),
        };
    }

    private async Task<VocabSampleVerseResponse?> VerseAsync(int entryId) =>
        _entries is null ? null : await _entries.GetSampleVerseAsync(entryId);

    /// <summary>The verse of a verse-context card. With gaps the German hit words (the rendering) are blanked, so the card does not give the answer away.</summary>
    private async Task<VocabSampleVerseResponse?> QuizVerseAsync(IDbConnection c, IDbTransaction t, VocabPendingCard pending, bool withGaps)
    {
        if (_entries is null)
        {
            return null;
        }

        var meanings = (await VocabSessionRepository.GetAcceptedMeaningsAsync(c, t, [pending.EntryId]))[pending.EntryId].Select(m => m.German).ToList();
        var quiz = await _entries.GetQuizVerseAsync(pending.EntryId, meanings, pending.VerseRefId);

        if (quiz is null || !withGaps)
        {
            return quiz?.Verse;
        }

        var verse = quiz.Verse;
        verse.German = verse.German.Select(w => w.IsHit ? new VocabSampleWordResponse { Position = w.Position, Text = string.Empty, IsHit = true, IsGap = true } : w).ToList();

        return verse;
    }

    /// <summary>The entry of a listening card: the audio is the prompt, so only the language and the audio URL are sent.</summary>
    private VocabCardEntryResponse ToListeningEntry(VocabSessionRepository.EntryRow e) => new()
    {
        Id = e.Id,
        Lang = Lang(e.Track),
        AudioUrl = VocabAudio.UrlFor(_storage, e.AudioObjectKey)
    };

    private VocabCardEntryResponse ToEntry(VocabSessionRepository.EntryRow e) => new()
    {
        Id = e.Id,
        Lemma = e.Lemma,
        DictionaryForm = e.DictionaryForm,
        Transliteration = e.Transliteration,
        Pos = e.Pos,
        Lang = Lang(e.Track),
        AudioUrl = VocabAudio.UrlFor(_storage, e.AudioObjectKey)
    };

    /// <summary>The entry of a reverse MC card: the lemma is the answer, so only what does not give it away is sent.</summary>
    private static VocabCardEntryResponse ToHiddenEntry(VocabSessionRepository.EntryRow e) => new()
    {
        Id = e.Id,
        Pos = e.Pos,
        Lang = Lang(e.Track),
    };

    private static string Lang(string track) => track == "G" ? "grc" : "he";

    private static string Truncate(string text, int max) => text.Length <= max ? text : text[..max];

    private static VocabSessionPlan Read(VocabSessionRepository.SessionRow session) =>
        JsonSerializer.Deserialize<VocabSessionPlan>(session.Plan, Json)!;

    private static VocabCard ToCard(VocabSessionRepository.DueCardRow row) => new(
        VocabCard.ParseState(row.State),
        row.Stability,
        row.Difficulty,
        new DateTimeOffset(DateTime.SpecifyKind(row.Due, DateTimeKind.Utc)),
        row.Reps,
        row.Lapses,
        row.LastReview is { } last ? new DateTimeOffset(DateTime.SpecifyKind(last, DateTimeKind.Utc)) : null);
}
