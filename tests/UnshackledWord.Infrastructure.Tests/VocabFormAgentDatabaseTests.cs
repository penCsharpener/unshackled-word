using System.Data;
using Dapper;
using Npgsql;
using UnshackledWord.Infrastructure.Vocab;

namespace UnshackledWord.Infrastructure.Tests;

/// <summary>
/// Roadmap 0022.13: the write side of the dictionary-form agent run (<see cref="VocabEntryEditor.ApplyAgentFormAsync(IDbConnection, IDbTransaction, int, string, int?)"/>)
/// and the maintainer's "accept form" against a real Postgres with the generated vocab lexicon (λόγος = G3056). One rolled-back transaction per test.
/// Set <c>UNSHACKLEDWORD_TEST_POSTGRES</c>, otherwise the tests are skipped.
/// </summary>
[Collection("VocabDatabase")]
public class VocabFormAgentDatabaseTests
{
    private const string EnvVar = "UNSHACKLEDWORD_TEST_POSTGRES";

    private sealed record EntryState(int Version, string? Form, string? Origin);

    private static async Task WithTransactionAsync(Func<IDbConnection, IDbTransaction, int, Task> test)
    {
        var connectionString = Environment.GetEnvironmentVariable(EnvVar);
        Assert.SkipWhen(string.IsNullOrWhiteSpace(connectionString), $"{EnvVar} is not set.");

        using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();
        var id = await connection.QuerySingleAsync<int>("""SELECT "VocabEntryId" FROM "unshackled-word"."VocabStrongs" WHERE "StrongsKey" = 'G3056'""", transaction: transaction);

        await test(connection, transaction, id);

        transaction.Rollback();
    }

    private static Task SetFormAsync(IDbConnection c, IDbTransaction t, int id, string? form, string? origin) =>
        c.ExecuteAsync("""UPDATE "unshackled-word"."VocabEntries" SET "DictionaryForm" = @Form, "DictionaryFormOrigin" = @Origin WHERE "Id" = @Id""", new { Id = id, Form = form, Origin = origin }, t);

    private static async Task<EntryState> StateAsync(IDbConnection c, IDbTransaction t, int id) =>
        await c.QuerySingleAsync<EntryState>("""SELECT e."ContentVersion" AS "Version", e."DictionaryForm" AS "Form", e."DictionaryFormOrigin" AS "Origin" FROM "unshackled-word"."VocabEntries" e WHERE e."Id" = @Id""", new { Id = id }, t);

    private static async Task<int> ChangeCountAsync(IDbConnection c, IDbTransaction t, int id) =>
        await c.QuerySingleAsync<int>("""SELECT count(*)::int FROM "unshackled-word"."VocabEntryChanges" WHERE "VocabEntryId" = @Id""", new { Id = id }, t);

    private static async Task<int> UserAsync(IDbConnection c, IDbTransaction t) =>
        await c.QueryFirstAsync<int>("""SELECT "Id" FROM "unshackled-word"."Users" ORDER BY "Id" LIMIT 1""", transaction: t);

    [Fact]
    public async Task Agent_FillsMissingForm_OriginAgent_BumpsOnce_WritesFormRow() => await WithTransactionAsync(async (c, t, id) =>
    {
        await SetFormAsync(c, t, id, null, null);
        var start = await StateAsync(c, t, id);
        var changes = await ChangeCountAsync(c, t, id);
        var ai = await UserAsync(c, t);

        var outcome = await VocabEntryEditor.ApplyAgentFormAsync(c, t, id, " λόγος, -ου, ὁ ", ai);

        outcome.Status.ShouldBe(VocabEditStatus.Ok);
        outcome.Response!.Bumped.ShouldBeTrue();
        var after = await StateAsync(c, t, id);
        after.Form.ShouldBe("λόγος, -ου, ὁ");
        after.Origin.ShouldBe("agent");
        after.Version.ShouldBe(start.Version + 1);
        (await ChangeCountAsync(c, t, id)).ShouldBe(changes + 1);
        (await c.QuerySingleAsync<int?>("""SELECT "UpdatedByUserId" FROM "unshackled-word"."VocabEntries" WHERE "Id" = @Id""", new { Id = id }, t)).ShouldBe(ai);
        (await c.QuerySingleAsync<string>("""SELECT "Kind" FROM "unshackled-word"."VocabEntryChanges" WHERE "VocabEntryId" = @Id ORDER BY "Id" DESC LIMIT 1""", new { Id = id }, t)).ShouldBe("form");
    });

    [Fact]
    public async Task Agent_OverwritesGeneratedForm_SameTextOnlyChangesOrigin() => await WithTransactionAsync(async (c, t, id) =>
    {
        await SetFormAsync(c, t, id, "λόγος, -ου, ὁ", "generated");
        var start = await StateAsync(c, t, id);
        var changes = await ChangeCountAsync(c, t, id);

        var same = await VocabEntryEditor.ApplyAgentFormAsync(c, t, id, "λόγος, -ου, ὁ", null);

        same.Status.ShouldBe(VocabEditStatus.Ok);
        same.Response!.Bumped.ShouldBeFalse();
        (await StateAsync(c, t, id)).ShouldBe(new EntryState(start.Version, "λόγος, -ου, ὁ", "agent"));
        (await ChangeCountAsync(c, t, id)).ShouldBe(changes);

        await SetFormAsync(c, t, id, "λόγος", "generated");
        var different = await VocabEntryEditor.ApplyAgentFormAsync(c, t, id, "λόγος, -ου, ὁ", null);

        different.Response!.Bumped.ShouldBeTrue();
        (await StateAsync(c, t, id)).Origin.ShouldBe("agent");
    });

    [Theory]
    [InlineData("manual")]
    [InlineData("agent")]
    public async Task Agent_NeverTouchesManualOrAgentForms(string origin) => await WithTransactionAsync(async (c, t, id) =>
    {
        await SetFormAsync(c, t, id, "λόγος (von Hand)", origin);
        var start = await StateAsync(c, t, id);
        var changes = await ChangeCountAsync(c, t, id);

        var outcome = await VocabEntryEditor.ApplyAgentFormAsync(c, t, id, "λόγος, -ου, ὁ", null);

        outcome.Status.ShouldBe(VocabEditStatus.Conflict);
        (await StateAsync(c, t, id)).ShouldBe(start);
        (await ChangeCountAsync(c, t, id)).ShouldBe(changes);
    });

    [Fact]
    public async Task Agent_UnknownEntryIsNotFound() => await WithTransactionAsync(async (c, t, _) =>
    {
        (await VocabEntryEditor.ApplyAgentFormAsync(c, t, -1, "λόγος", null)).Status.ShouldBe(VocabEditStatus.NotFound);
    });

    [Fact]
    public async Task AcceptForm_SetsManual_NoBumpNoChangeRow_RefusesWithoutForm() => await WithTransactionAsync(async (c, t, id) =>
    {
        await SetFormAsync(c, t, id, "λόγος, -ου, ὁ", "agent");
        var start = await StateAsync(c, t, id);
        var changes = await ChangeCountAsync(c, t, id);

        var accepted = await VocabEntryEditor.AcceptFormAsync(c, t, id, null);

        accepted.Status.ShouldBe(VocabEditStatus.Ok);
        accepted.Response!.Bumped.ShouldBeFalse();
        (await StateAsync(c, t, id)).ShouldBe(new EntryState(start.Version, "λόγος, -ου, ὁ", "manual"));
        (await ChangeCountAsync(c, t, id)).ShouldBe(changes);

        await SetFormAsync(c, t, id, null, null);
        (await VocabEntryEditor.AcceptFormAsync(c, t, id, null)).Status.ShouldBe(VocabEditStatus.Conflict);
    });
}
