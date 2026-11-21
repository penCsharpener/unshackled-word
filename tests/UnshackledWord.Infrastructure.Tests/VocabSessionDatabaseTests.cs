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
            last = Ok(await service.AnswerAsync(c, t, userId, start.SessionId, card.Seq, card.Kind == "multiple_choice" ? 0 : null, null));

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

            Ok(await service.AnswerAsync(c, t, userId, start.SessionId, next.Card!.Seq, next.Card.Kind == "multiple_choice" ? 0 : null, null));
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
            Ok(await service.AnswerAsync(c, t, userId, second.SessionId, card.Seq, card.Kind == "multiple_choice" ? 0 : null, null));
        }

        // a due repeat may slip in before the last review word, but no introduction comes before each review word was asked once
        var firstIntro = kinds.FindIndex(k => k.Kind == "introduction");
        firstIntro.ShouldBeGreaterThanOrEqualTo(second.ReviewWords);
        kinds.Take(firstIntro).ShouldAllBe(k => k.Kind == "multiple_choice");
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
}
