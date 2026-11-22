using System.Data;
using System.Text.Json;
using Dapper;
using Npgsql;
using UnshackledWord.Application.Features.Vocab;
using UnshackledWord.Application.Features.Vocab.Session;
using UnshackledWord.Domain.WebApi.Vocab;
using UnshackledWord.Infrastructure.Vocab;

namespace UnshackledWord.Infrastructure.Tests;

/// <summary>
/// Roadmap 0022.8 (session engine part 1) against a real Postgres with the derived NT lexicon (ranks 1–4: ὁ, καί, αὐτός, σύ). Every test runs in
/// one transaction that is rolled back and uses its own new user. Set <c>UNSHACKLEDWORD_TEST_POSTGRES</c>, otherwise the tests are skipped.
/// </summary>
[Collection("VocabDatabase")]
public class VocabSessionDatabaseTests
{
    private const string EnvVar = "UNSHACKLEDWORD_TEST_POSTGRES";
    private const string Course = "nt-greek";
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static readonly DateTimeOffset T0 = new(2026, 3, 1, 8, 0, 0, TimeSpan.Zero);

    private static async Task WithTransactionAsync(Func<IDbConnection, IDbTransaction, Task> test)
    {
        var connectionString = Environment.GetEnvironmentVariable(EnvVar);
        Assert.SkipWhen(string.IsNullOrWhiteSpace(connectionString), $"{EnvVar} is not set.");

        using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();

        await test(connection, transaction);

        transaction.Rollback();
    }

    private static async Task<int> NewUserAsync(IDbConnection c, IDbTransaction t)
    {
        var name = "vocab-session-" + Guid.NewGuid().ToString("N");

        return await c.QuerySingleAsync<int>("""
            INSERT INTO "unshackled-word"."Users" ("Email", "EmailNormalized", "DisplayName")
            VALUES (@Name || '@example.org', @Name || '@example.org', @Name)
            RETURNING "Id"
            """, new { Name = name }, t);
    }

    private static VocabSessionService Service(FixedTime time, int seed = 1) => new(null!, new VocabScheduler(), time, null, new Random(seed));

    private static T Ok<T>(VocabSessionOutcome<T> outcome) where T : class
    {
        outcome.Status.ShouldBe(VocabSessionStatus.Ok, outcome.Message);

        return outcome.Response!;
    }

    private static async Task<List<string>> LemmasByRankAsync(IDbConnection c, IDbTransaction t, int count) =>
        (await c.QueryAsync<string>("""
            SELECT e."Lemma" FROM "unshackled-word"."VocabEntries" e WHERE e."Track" = 'G' AND e."FrequencyRank" BETWEEN 1 AND @Count ORDER BY e."FrequencyRank"
            """, new { Count = count }, t)).ToList();

    [Fact]
    public async Task NewUser_GetsIntroductionsOfTheTopFourWords_ThenMultipleChoice_AndNextNeverLeaksTheAnswer() => await WithTransactionAsync(async (c, t) =>
    {
        var userId = await NewUserAsync(c, t);
        var service = Service(new FixedTime(T0));
        var start = Ok(await service.StartAsync(c, t, userId, Course));

        start.Resumed.ShouldBeFalse();
        start.NewWords.ShouldBe(10);
        start.ReviewWords.ShouldBe(0);

        var top = await LemmasByRankAsync(c, t, 4);
        var seen = new List<VocabCardResponse>();

        for (var i = 0; i < 12; i++)
        {
            var next = Ok(await service.NextAsync(c, t, userId, start.SessionId));
            var card = next.Card!;
            seen.Add(card);

            // idempotent: asking again returns the same card and the same options in the same order
            var again = Ok(await service.NextAsync(c, t, userId, start.SessionId)).Card!;
            JsonSerializer.Serialize(again, Web).ShouldBe(JsonSerializer.Serialize(card, Web));

            // the payload says nothing about which option is right
            var json = JsonSerializer.Serialize(next, Web).ToLowerInvariant();
            json.ShouldNotContain("correct");
            json.ShouldNotContain("iscorrect");

            if (card.Kind == "introduction")
            {
                card.Options.ShouldBeNull();
                card.Meanings!.Count.ShouldBeGreaterThan(0);
                Ok(await service.AnswerAsync(c, t, userId, start.SessionId, card.Seq, null, 1200));
            }
            else
            {
                card.Options!.Count.ShouldBe(4);
                card.Options.Select(o => o.Text).Distinct(StringComparer.OrdinalIgnoreCase).Count().ShouldBe(4);
                card.Meanings.ShouldBeNull();
                var answer = Ok(await service.AnswerAsync(c, t, userId, start.SessionId, card.Seq, 0, 900));
                answer.Seq.ShouldBe(card.Seq);
            }
        }

        seen.Take(4).Select(x => x.Kind).ShouldAllBe(k => k == "introduction");
        seen.Take(4).Select(x => x.Entry.Lemma).ShouldBe(top);
        seen.Skip(4).ShouldAllBe(x => x.Kind == "multiple_choice");
        seen.Skip(4).Select(x => x.Entry.Id).Distinct().Count().ShouldBeGreaterThan(1);
    });

    [Fact]
    public async Task AnswerIsJudgedOnTheServer_AndWritesCardAndLog() => await WithTransactionAsync(async (c, t) =>
    {
        var userId = await NewUserAsync(c, t);
        var time = new FixedTime(T0);
        var service = Service(time);
        var start = Ok(await service.StartAsync(c, t, userId, Course));

        // introductions of the first wave
        for (var i = 0; i < 4; i++)
        {
            var intro = Ok(await service.NextAsync(c, t, userId, start.SessionId)).Card!;
            var introAnswer = Ok(await service.AnswerAsync(c, t, userId, start.SessionId, intro.Seq, null, 500));
            introAnswer.Strength.ShouldBeNull();
            introAnswer.Correct.ShouldBeTrue();
        }

        (await c.QuerySingleAsync<int>("""SELECT count(*)::int FROM "unshackled-word"."VocabCards" WHERE "UserId" = @UserId AND "State" = 'new'""", new { UserId = userId }, t)).ShouldBe(4);

        var question = Ok(await service.NextAsync(c, t, userId, start.SessionId)).Card!;
        question.Kind.ShouldBe("multiple_choice");

        // a stale seq and an unknown option are refused
        (await service.AnswerAsync(c, t, userId, start.SessionId, question.Seq + 5, 0, null)).Status.ShouldBe(VocabSessionStatus.Conflict);
        (await service.AnswerAsync(c, t, userId, start.SessionId, question.Seq, 4, null)).Status.ShouldBe(VocabSessionStatus.Invalid);

        // find the right option through the verdict of a first wrong pick, then answer the next question of the same kind correctly
        var wrong = Ok(await service.AnswerAsync(c, t, userId, start.SessionId, question.Seq, 0, 700));
        var rightIndex = wrong.CorrectOptionId!.Value;

        if (wrong.Correct)
        {
            wrong.CorrectOptionId.ShouldBe(0);
        }
        else
        {
            wrong.Given.ShouldBe(question.Options![0].Text);
            wrong.Strength!.After.ShouldBeLessThanOrEqualTo(wrong.Strength.Before + 0.0001);
        }

        wrong.CorrectAnswers.ShouldContain(question.Options![rightIndex].Text);

        var log = (await c.QueryAsync<(string QuizType, bool IsCorrect, int? Rating, int? ResponseMs, string State)>("""
            SELECT "QuizType", "IsCorrect", "Rating", "ResponseMs", "StateBefore" FROM "unshackled-word"."VocabReviewLog" WHERE "UserId" = @UserId ORDER BY "Id"
            """, new { UserId = userId }, t)).ToList();
        log.Count(l => l.QuizType == "introduction").ShouldBe(4);
        var mc = log.Single(l => l.QuizType == "multiple_choice");
        mc.Rating.ShouldBe(mc.IsCorrect ? 3 : 1);
        mc.ResponseMs.ShouldBe(700);
        mc.State.ShouldBe("new");

        var card = await c.QuerySingleAsync<(string State, int Reps, DateTime? LastReview, int Seen)>("""
            SELECT "State", "Reps", "LastReview", "SeenContentVersion" FROM "unshackled-word"."VocabCards" WHERE "UserId" = @UserId AND "VocabEntryId" = @Id
            """, new { UserId = userId, Id = question.Entry.Id }, t);
        card.Reps.ShouldBe(1);
        card.LastReview.ShouldNotBeNull();
        card.State.ShouldBe(wrong.Correct ? "review" : "learning");
    });

    [Fact]
    public async Task FullSession_EndsWithAllWordsQuizzedThreeTimes_AndTheOpenSessionIsResumed() => await WithTransactionAsync(async (c, t) =>
    {
        var userId = await NewUserAsync(c, t);
        var service = Service(new FixedTime(T0), 7);
        var start = Ok(await service.StartAsync(c, t, userId, Course));
        var resumed = Ok(await service.StartAsync(c, t, userId, Course));

        resumed.Resumed.ShouldBeTrue();
        resumed.SessionId.ShouldBe(start.SessionId);

        VocabAnswerResponse? last = null;

        for (var i = 0; i < 200; i++)
        {
            var next = Ok(await service.NextAsync(c, t, userId, start.SessionId));

            if (next.Finished)
            {
                break;
            }

            var card = next.Card!;
            last = Ok(await service.AnswerAsync(c, t, userId, start.SessionId, card.Seq, card.Kind != "introduction" ? 0 : null, null));

            if (last.Finished)
            {
                break;
            }
        }

        last!.Finished.ShouldBeTrue();
        last.Summary!.NewWords.ShouldBe(10);
        last.Summary.Answers.ShouldBeGreaterThanOrEqualTo(30);
        last.Progress.OpenWave.ShouldBe(0);
        last.Progress.WordsDone.ShouldBe(10);

        var perWord = await c.QueryAsync<int>("""
            SELECT count(*)::int FROM "unshackled-word"."VocabReviewLog" WHERE "UserId" = @UserId AND "QuizType" = 'multiple_choice' GROUP BY "VocabEntryId"
            """, new { UserId = userId }, t);
        perWord.ShouldAllBe(n => n >= 3);

        // a finished session answers next with the summary and no card; a new start opens a new session
        var after = Ok(await service.NextAsync(c, t, userId, start.SessionId));
        after.Finished.ShouldBeTrue();
        after.Card.ShouldBeNull();
        after.Summary!.NewWords.ShouldBe(10);

        // another user cannot touch it
        var other = await NewUserAsync(c, t);
        (await service.NextAsync(c, t, other, start.SessionId)).Status.ShouldBe(VocabSessionStatus.NotFound);
    });

    [Fact]
    public async Task NextDay_TheDueCardsComeBackFirst_AsReviews() => await WithTransactionAsync(async (c, t) =>
    {
        var userId = await NewUserAsync(c, t);
        var time = new FixedTime(T0);
        var service = Service(time, 3);
        var start = Ok(await service.StartAsync(c, t, userId, Course));

        while (true)
        {
            var next = Ok(await service.NextAsync(c, t, userId, start.SessionId));

            if (next.Finished)
            {
                break;
            }

            Ok(await service.AnswerAsync(c, t, userId, start.SessionId, next.Card!.Seq, next.Card.Kind != "introduction" ? 0 : null, null));
        }

        time.Now = T0.AddDays(3);
        var second = Ok(await service.StartAsync(c, t, userId, Course));

        second.SessionId.ShouldNotBe(start.SessionId);
        second.ReviewWords.ShouldBeGreaterThan(0);
        second.NewWords.ShouldBe(Math.Max(4, 20 - second.ReviewWords));

        // reviews first: every due word is asked (multiple choice) before the first introduction
        var kinds = new List<(string Kind, int EntryId)>();

        for (var i = 0; i < second.ReviewWords * 2 + 8; i++)
        {
            var card = Ok(await service.NextAsync(c, t, userId, second.SessionId)).Card!;
            kinds.Add((card.Kind, card.Entry.Id));
            Ok(await service.AnswerAsync(c, t, userId, second.SessionId, card.Seq, card.Kind != "introduction" ? 0 : null, null));
        }

        // a due repeat may slip in before the last review word, but no introduction comes before each review word was asked once
        var firstIntro = kinds.FindIndex(k => k.Kind == "introduction");
        firstIntro.ShouldBeGreaterThanOrEqualTo(second.ReviewWords);
        kinds.Take(firstIntro).ShouldAllBe(k => k.Kind == "multiple_choice" || k.Kind == "reverse_multiple_choice");
        kinds.Take(firstIntro).Select(k => k.EntryId).Distinct().Count().ShouldBe(second.ReviewWords);
    });

    [Fact]
    public async Task RetiredEntry_IsNotQuizzedAndNotIntroduced() => await WithTransactionAsync(async (c, t) =>
    {
        var userId = await NewUserAsync(c, t);
        var service = Service(new FixedTime(T0));
        var start = Ok(await service.StartAsync(c, t, userId, Course));
        var secondLemma = (await LemmasByRankAsync(c, t, 2))[1];

        // the maintainer retires the entry of rank 1 after the session started
        await c.ExecuteAsync("""
            UPDATE "unshackled-word"."VocabEntries" SET "RetiredAt" = now(), "FrequencyRank" = NULL WHERE "Track" = 'G' AND "FrequencyRank" = 1
            """, transaction: t);

        var first = Ok(await service.NextAsync(c, t, userId, start.SessionId)).Card!;

        first.Entry.Lemma.ShouldBe(secondLemma);
    });

    [Fact]
    public async Task Distractor_NeverIsAnEntryThatAlsoMeansTheSame_EvenWhenItIsInTheRankBand() => await WithTransactionAsync(async (c, t) =>
    {
        // the prompt entry: a verb of the NT course; the "twin": another verb in its rank band whose top meaning is made to equal one of the prompt's meanings
        var prompt = await c.QueryFirstAsync<(int Id, int Rank, string Pos)>("""
            SELECT e."Id", e."FrequencyRank", e."Pos"
            FROM "unshackled-word"."VocabEntries" e
            WHERE e."Track" = 'G' AND e."Pos" = 'G:V' AND e."FrequencyRank" > 50 AND NOT e."IsMorpheme" AND e."RetiredAt" IS NULL
                AND EXISTS (SELECT 1 FROM "unshackled-word"."VocabMeanings" m WHERE m."VocabEntryId" = e."Id" AND m."IsAccepted")
            ORDER BY e."FrequencyRank"
            LIMIT 1
            """, transaction: t);
        var promptMeanings = (await c.QueryAsync<string>("""
            SELECT m."German" FROM "unshackled-word"."VocabMeanings" m WHERE m."VocabEntryId" = @Id AND m."IsAccepted" ORDER BY m."Rank"
            """, new { prompt.Id }, t)).ToList();
        var twin = await c.QueryFirstAsync<int>("""
            SELECT e."Id"
            FROM "unshackled-word"."VocabEntries" e
            WHERE e."Track" = 'G' AND e."Pos" = @Pos AND e."FrequencyRank" BETWEEN @From AND @To AND e."Id" <> @Id AND e."RetiredAt" IS NULL
                AND EXISTS (SELECT 1 FROM "unshackled-word"."VocabMeanings" m WHERE m."VocabEntryId" = e."Id" AND m."IsAccepted")
            ORDER BY e."FrequencyRank"
            LIMIT 1
            """, new { prompt.Pos, From = prompt.Rank - 200, To = prompt.Rank + 200, prompt.Id }, t);

        // like λέγω / εἶπον: the twin's top-ranked accepted meaning is "Sagen" in different case, which the prompt entry accepts as "sagen"
        await c.ExecuteAsync("""
            UPDATE "unshackled-word"."VocabMeanings" SET "German" = @German
            WHERE "Id" = (SELECT m."Id" FROM "unshackled-word"."VocabMeanings" m WHERE m."VocabEntryId" = @Id AND m."IsAccepted" ORDER BY m."Rank" LIMIT 1)
            """, new { German = promptMeanings[0].ToUpperInvariant(), Id = twin }, t);

        var pool = await VocabSessionRepository.GetDistractorPoolAsync(c, t, "G", prompt.Id, prompt.Pos, prompt.Rank, []);

        pool.ShouldContain(p => p.EntryId == twin);

        var promptEntry = new VocabDistractorPrompt(prompt.Id, prompt.Pos, prompt.Rank, promptMeanings[0], promptMeanings);

        for (var seed = 0; seed < 30; seed++)
        {
            var picked = VocabDistractorPicker.Pick(promptEntry, pool, new HashSet<int>(), 3, new Random(seed));

            picked.Count.ShouldBe(3);
            picked.ShouldNotContain(p => p.EntryId == twin);
            picked.ShouldAllBe(p => !promptMeanings.Select(VocabDistractorPicker.Fold).Contains(VocabDistractorPicker.Fold(p.TopMeaning)));
        }
    });

    private static async Task<(VocabSessionService Service, FixedTime Time, int UserId)> UserWithFinishedFirstSessionAsync(IDbConnection c, IDbTransaction t, int seed)
    {
        var userId = await NewUserAsync(c, t);
        var time = new FixedTime(T0);
        var service = Service(time, seed);
        var start = Ok(await service.StartAsync(c, t, userId, Course));

        while (true)
        {
            var next = Ok(await service.NextAsync(c, t, userId, start.SessionId));

            if (next.Finished)
            {
                break;
            }

            next.Card!.Kind.ShouldBeOneOf("introduction", "multiple_choice");
            Ok(await service.AnswerAsync(c, t, userId, start.SessionId, next.Card.Seq, next.Card.Kind != "introduction" ? 0 : null, null));
        }

        return (service, time, userId);
    }

    /// <summary>Starts the next session three days later with every card created <paramref name="cardAgeHours"/> hours before that moment and plays it to the end.</summary>
    private static async Task<List<VocabCardResponse>> PlayReviewSessionAsync(IDbConnection c, IDbTransaction t, VocabSessionService service, FixedTime time, int userId, int cardAgeHours)
    {
        time.Now = T0.AddDays(3);
        await c.ExecuteAsync("""UPDATE "unshackled-word"."VocabCards" SET "CreatedAt" = @CreatedAt WHERE "UserId" = @UserId""",
            new { CreatedAt = time.Now.UtcDateTime.AddHours(-cardAgeHours), UserId = userId }, t);
        var start = Ok(await service.StartAsync(c, t, userId, Course));
        var cards = new List<VocabCardResponse>();

        for (var i = 0; i < 300; i++)
        {
            var next = Ok(await service.NextAsync(c, t, userId, start.SessionId));

            if (next.Finished)
            {
                break;
            }

            cards.Add(next.Card!);
            var answer = await service.AnswerAsync(c, t, userId, start.SessionId, next.Card!.Seq, next.Card.Kind != "introduction" ? 0 : null, null);
            Ok(answer);
        }

        return cards;
    }

    [Fact]
    public async Task ACardCreatedYesterday_CanGetReverseMultipleChoice_AndTheCardNeverRevealsTheAnswer() => await WithTransactionAsync(async (c, t) =>
    {
        var (service, time, userId) = await UserWithFinishedFirstSessionAsync(c, t, 5);

        time.Now = T0.AddDays(3);
        await c.ExecuteAsync("""UPDATE "unshackled-word"."VocabCards" SET "CreatedAt" = @CreatedAt WHERE "UserId" = @UserId""",
            new { CreatedAt = time.Now.UtcDateTime.AddHours(-25), UserId = userId }, t);
        var start = Ok(await service.StartAsync(c, t, userId, Course));
        var seenReverse = false;

        for (var i = 0; i < 300 && !seenReverse; i++)
        {
            var next = Ok(await service.NextAsync(c, t, userId, start.SessionId));
            var card = next.Card!;

            if (card.Kind == "reverse_multiple_choice")
            {
                seenReverse = true;
                var again = Ok(await service.NextAsync(c, t, userId, start.SessionId)).Card!;
                JsonSerializer.Serialize(again, Web).ShouldBe(JsonSerializer.Serialize(card, Web));

                card.Prompt.ShouldNotBeNullOrWhiteSpace();
                card.Options!.Count.ShouldBe(4);
                card.Options.ShouldAllBe(o => o.Lang == "grc");
                card.Options.Select(o => o.Text).Distinct(StringComparer.OrdinalIgnoreCase).Count().ShouldBe(4);
                card.Entry.Lemma.ShouldBeEmpty();
                card.Entry.DictionaryForm.ShouldBeNull();
                JsonSerializer.Serialize(next, Web).ToLowerInvariant().ShouldNotContain("correct");

                var entry = await c.QuerySingleAsync<(string Lemma, string? Form)>("""SELECT "Lemma", "DictionaryForm" FROM "unshackled-word"."VocabEntries" WHERE "Id" = @Id""", new { Id = card.Entry.Id }, t);
                var form = entry.Form ?? entry.Lemma;
                var topMeaning = await c.QuerySingleAsync<string>("""SELECT "German" FROM "unshackled-word"."VocabMeanings" WHERE "VocabEntryId" = @Id AND "IsAccepted" ORDER BY "Rank" LIMIT 1""", new { Id = card.Entry.Id }, t);
                card.Prompt.ShouldBe(topMeaning);

                var answer = Ok(await service.AnswerAsync(c, t, userId, start.SessionId, card.Seq, 0, 800));
                answer.Kind.ShouldBe("reverse_multiple_choice");
                answer.CorrectAnswers.ShouldBe([form]);
                card.Options[answer.CorrectOptionId!.Value].Text.ShouldBe(form);
                answer.Given.ShouldBe(card.Options[0].Text);
                answer.Correct.ShouldBe(answer.CorrectOptionId == 0);
                answer.Entry.Lemma.ShouldBe(entry.Lemma);

                // a distractor lemma never has the prompt meaning among its accepted meanings
                var others = card.Options.Where((_, index) => index != answer.CorrectOptionId).Select(o => o.Text).ToList();
                var clashes = await c.QuerySingleAsync<int>("""
                    SELECT count(*)::int
                    FROM "unshackled-word"."VocabEntries" e
                        JOIN "unshackled-word"."VocabMeanings" m ON m."VocabEntryId" = e."Id" AND m."IsAccepted"
                    WHERE e."Track" = 'G' AND COALESCE(e."DictionaryForm", e."Lemma") = ANY(@Forms) AND lower(m."German") = lower(@Prompt)
                    """, new { Forms = others.ToArray(), Prompt = topMeaning }, t);
                clashes.ShouldBe(0);

                (await c.QuerySingleAsync<int>("""SELECT count(*)::int FROM "unshackled-word"."VocabReviewLog" WHERE "UserId" = @UserId AND "QuizType" = 'reverse_multiple_choice'""", new { UserId = userId }, t)).ShouldBe(1);
            }
            else
            {
                Ok(await service.AnswerAsync(c, t, userId, start.SessionId, card.Seq, card.Kind != "introduction" ? 0 : null, null));
            }
        }

        seenReverse.ShouldBeTrue();
    });

    [Fact]
    public async Task ACardCreatedToday_NeverGetsReverseMultipleChoice() => await WithTransactionAsync(async (c, t) =>
    {
        var (service, time, userId) = await UserWithFinishedFirstSessionAsync(c, t, 5);
        var cards = await PlayReviewSessionAsync(c, t, service, time, userId, cardAgeHours: 2);

        cards.Where(card => card.Kind != "introduction").ShouldAllBe(card => card.Kind == "multiple_choice");
    });

    [Fact]
    public async Task WithReverseMultipleChoiceDisabled_ItIsNeverAsked() => await WithTransactionAsync(async (c, t) =>
    {
        var (service, time, userId) = await UserWithFinishedFirstSessionAsync(c, t, 5);
        var saved = await VocabSettingsService.SetAsync(c, t, userId, ["reverse_multiple_choice", "reverse_multiple_choice"]);

        saved.DisabledQuizTypes.ShouldBe(["reverse_multiple_choice"]);
        saved.AvailableQuizTypes.ShouldBe(["multiple_choice", "reverse_multiple_choice"]);
        (await VocabSettingsService.GetAsync(c, t, userId)).DisabledQuizTypes.ShouldBe(["reverse_multiple_choice"]);

        var cards = await PlayReviewSessionAsync(c, t, service, time, userId, cardAgeHours: 80);

        cards.Count(card => card.Kind == "multiple_choice").ShouldBeGreaterThan(10);
        cards.ShouldNotContain(card => card.Kind == "reverse_multiple_choice");
    });

    [Fact]
    public async Task WithMultipleChoiceDisabledOnDayZero_TheAgeIsIgnoredAndReverseMultipleChoiceIsAsked() => await WithTransactionAsync(async (c, t) =>
    {
        var (service, time, userId) = await UserWithFinishedFirstSessionAsync(c, t, 5);
        await VocabSettingsService.SetAsync(c, t, userId, ["multiple_choice"]);

        var cards = await PlayReviewSessionAsync(c, t, service, time, userId, cardAgeHours: 1);
        var quizzes = cards.Where(card => card.Kind != "introduction").ToList();

        quizzes.ShouldNotBeEmpty();
        quizzes.ShouldAllBe(card => card.Kind == "reverse_multiple_choice");
    });

    [Fact]
    public async Task Settings_DefaultToNothingDisabled_AndAreStoredPerUser() => await WithTransactionAsync(async (c, t) =>
    {
        var userId = await NewUserAsync(c, t);
        var other = await NewUserAsync(c, t);

        var initial = await VocabSettingsService.GetAsync(c, t, userId);
        initial.DisabledQuizTypes.ShouldBeEmpty();
        initial.AvailableQuizTypes.ShouldBe(["multiple_choice", "reverse_multiple_choice"]);

        await VocabSettingsService.SetAsync(c, t, userId, ["listening", "reverse_multiple_choice"]);
        (await VocabSettingsService.GetAsync(c, t, userId)).DisabledQuizTypes.ShouldBe(["reverse_multiple_choice", "listening"]);
        (await VocabSettingsService.GetAsync(c, t, other)).DisabledQuizTypes.ShouldBeEmpty();

        await VocabSettingsService.SetAsync(c, t, userId, []);
        (await VocabSettingsService.GetAsync(c, t, userId)).DisabledQuizTypes.ShouldBeEmpty();
    });

    [Fact]
    public async Task ReverseDistractor_NeverHasThePromptMeaningAmongItsAcceptedMeanings_InTheRealPool() => await WithTransactionAsync(async (c, t) =>
    {
        var prompt = await c.QueryFirstAsync<(int Id, int Rank, string Pos, string Lemma, string? Form)>("""
            SELECT e."Id", e."FrequencyRank", e."Pos", e."Lemma", e."DictionaryForm"
            FROM "unshackled-word"."VocabEntries" e
            WHERE e."Track" = 'G' AND e."Pos" = 'G:V' AND e."FrequencyRank" > 50 AND NOT e."IsMorpheme" AND e."RetiredAt" IS NULL
                AND EXISTS (SELECT 1 FROM "unshackled-word"."VocabMeanings" m WHERE m."VocabEntryId" = e."Id" AND m."IsAccepted")
            ORDER BY e."FrequencyRank"
            LIMIT 1
            """, transaction: t);
        var meanings = (await c.QueryAsync<string>("""
            SELECT m."German" FROM "unshackled-word"."VocabMeanings" m WHERE m."VocabEntryId" = @Id AND m."IsAccepted" ORDER BY m."Rank"
            """, new { prompt.Id }, t)).ToList();
        var twin = await c.QueryFirstAsync<int>("""
            SELECT e."Id"
            FROM "unshackled-word"."VocabEntries" e
            WHERE e."Track" = 'G' AND e."Pos" = @Pos AND e."FrequencyRank" BETWEEN @From AND @To AND e."Id" <> @Id AND e."RetiredAt" IS NULL
                AND EXISTS (SELECT 1 FROM "unshackled-word"."VocabMeanings" m WHERE m."VocabEntryId" = e."Id" AND m."IsAccepted")
            ORDER BY e."FrequencyRank"
            LIMIT 1
            """, new { prompt.Pos, From = prompt.Rank - 200, To = prompt.Rank + 200, prompt.Id }, t);

        // like εἶπον: the twin gets the prompt meaning as an additional accepted meaning (different case), while its top meaning stays another one
        await c.ExecuteAsync("""
            INSERT INTO "unshackled-word"."VocabMeanings" ("VocabEntryId", "German", "Rank", "IsAccepted", "Origin")
            VALUES (@Id, @German, 99, true, 'manual')
            """, new { Id = twin, German = meanings[0].ToUpperInvariant() }, t);

        var pool = await VocabSessionRepository.GetDistractorPoolAsync(c, t, "G", prompt.Id, prompt.Pos, prompt.Rank, []);
        pool.ShouldContain(p => p.EntryId == twin);

        var promptEntry = new VocabDistractorPrompt(prompt.Id, prompt.Pos, prompt.Rank, meanings[0], meanings);

        for (var seed = 0; seed < 30; seed++)
        {
            var picked = VocabDistractorPicker.PickLemmas(promptEntry, prompt.Form ?? prompt.Lemma, pool, new HashSet<int>(), 3, new Random(seed));

            picked.Count.ShouldBe(3);
            picked.ShouldNotContain(p => p.EntryId == twin);
            picked.ShouldAllBe(p => p.Form != null && !p.AcceptedMeanings!.Select(VocabDistractorPicker.Fold).Contains(VocabDistractorPicker.Fold(meanings[0])));
        }
    });
}
