using System.Data;
using Dapper;
using Npgsql;
using UnshackledWord.Application.Features.Notes;
using UnshackledWord.Infrastructure.Notes;

namespace UnshackledWord.Infrastructure.Tests;

/// <summary>
/// Roadmap 0023.1: the check constraints of <c>Notes</c> (migration 0042) and the visibility predicate of <see cref="NoteRepository"/> against a real Postgres.
/// Every test runs in one transaction that is rolled back and uses its own new users. Point <c>UNSHACKLEDWORD_TEST_POSTGRES</c> at a COPY of the dev
/// database (never the dev database itself), otherwise the tests are skipped.
/// </summary>
public class NoteDatabaseTests
{
    private const string EnvVar = "UNSHACKLEDWORD_TEST_POSTGRES";
    private const int Rom828 = 45008028;

    private static readonly NoteAnchor VerseAnchor = new(NoteKinds.Verse, 45, 8, Rom828, Rom828, null, null, null);
    private static readonly NoteAnchor ChapterAnchor = new(NoteKinds.Chapter, 45, 8, null, null, null, null, null);

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

    private static async Task<int> NewUserAsync(IDbConnection c, IDbTransaction t, string prefix = "notes")
    {
        var name = prefix + "-" + Guid.NewGuid().ToString("N");

        return await c.QuerySingleAsync<int>("""
            INSERT INTO "unshackled-word"."Users" ("Email", "EmailNormalized", "DisplayName")
            VALUES (@Name || '@example.org', @Name || '@example.org', @Name)
            RETURNING "Id"
            """, new { Name = name }, t);
    }

    private static Task<Guid> InsertRawAsync(IDbConnection c, IDbTransaction t, int userId, string kind, int book, int chapter, int? start, int? end,
        int? startPosition = null, int? endPosition = null, string? quoted = null, string visibility = NoteVisibilities.Private,
        string publicState = NotePublicStates.None, string body = "Notiz", string? approvedBody = null, bool hidden = false) =>
        c.QuerySingleAsync<Guid>("""
            INSERT INTO "unshackled-word"."Notes" ("UserId", "Kind", "BibleBookId", "Chapter", "StartHebRefId", "EndHebRefId", "StartPosition",
                                                   "EndPosition", "QuotedText", "BodyMarkdown", "Visibility", "PublicState", "ApprovedBodyMarkdown", "HiddenAt")
            VALUES (@UserId, @Kind, @Book, @Chapter, @Start, @End, @StartPosition,
                    @EndPosition, @Quoted, @Body, @Visibility, @PublicState, @ApprovedBody, CASE WHEN @Hidden THEN now() END)
            RETURNING "Id"
            """, new
        {
            UserId = userId, Kind = kind, Book = book, Chapter = chapter, Start = start, End = end, StartPosition = startPosition,
            EndPosition = endPosition, Quoted = quoted, Body = body, Visibility = visibility, PublicState = publicState, ApprovedBody = approvedBody,
            Hidden = hidden,
        }, t);

    public static TheoryData<string, string, int, int, int?, int?, int?, int?, string?, string, string> RefusedRows => new()
    {
        // name, kind, book, chapter, start, end, startPosition, endPosition, quoted, visibility, publicState
        { "verse range across chapters (Röm 8,39–9,1)", NoteKinds.Verses, 45, 8, 45008039, 45009001, null, null, null, "private", "none" },
        { "verse range backwards", NoteKinds.Verses, 45, 8, 45008030, 45008028, null, null, null, "private", "none" },
        { "verse note with another end", NoteKinds.Verse, 45, 8, 45008028, 45008029, null, null, null, "private", "none" },
        { "verse note without verse", NoteKinds.Verse, 45, 8, null, null, null, null, null, "private", "none" },
        { "verse note without end", NoteKinds.Verse, 45, 8, 45008028, null, null, null, null, "private", "none" },
        { "words note without end position", NoteKinds.Words, 45, 8, 45008028, 45008028, 2, null, "x", "private", "none" },
        { "chapter note with a verse", NoteKinds.Chapter, 45, 8, 45008028, 45008028, null, null, null, "private", "none" },
        { "verse outside the note's chapter", NoteKinds.Verse, 45, 9, 45008028, 45008028, null, null, null, "private", "none" },
        { "words note without quote", NoteKinds.Words, 45, 8, 45008028, 45008028, 2, 4, null, "private", "none" },
        { "words note with positions backwards", NoteKinds.Words, 45, 8, 45008028, 45008028, 4, 2, "x", "private", "none" },
        { "unknown kind", "paragraph", 45, 8, null, null, null, null, null, "private", "none" },
        { "public without public state", NoteKinds.Chapter, 45, 8, null, null, null, null, null, "public", "none" },
        { "private with public state", NoteKinds.Chapter, 45, 8, null, null, null, null, null, "private", "pending" },
    };

    [Theory]
    [MemberData(nameof(RefusedRows))]
    public async Task Insert_ShouldBeRefusedByACheckConstraint_WhenTheRowBreaksTheModel(string name, string kind, int book, int chapter, int? start, int? end,
        int? startPosition, int? endPosition, string? quoted, string visibility, string publicState) => await WithTransactionAsync(async (c, t) =>
    {
        var userId = await NewUserAsync(c, t);

        var error = await Should.ThrowAsync<PostgresException>(() =>
            InsertRawAsync(c, t, userId, kind, book, chapter, start, end, startPosition, endPosition, quoted, visibility, publicState));

        error.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation, name);
    });

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task Insert_ShouldBeRefused_WhenTheBodyIsEmptyOrTooLong(string? body) => await WithTransactionAsync(async (c, t) =>
    {
        var userId = await NewUserAsync(c, t);

        var error = await Should.ThrowAsync<PostgresException>(() =>
            InsertRawAsync(c, t, userId, NoteKinds.Chapter, 45, 8, null, null, body: body ?? new string('x', NoteBody.MaxLength + 1)));

        error.ConstraintName.ShouldBe("Notes_Body_CK");
    });

    [Fact]
    public async Task Insert_ShouldAcceptEveryKind_WhenTheRowFitsTheModel() => await WithTransactionAsync(async (c, t) =>
    {
        var userId = await NewUserAsync(c, t);

        await InsertRawAsync(c, t, userId, NoteKinds.Chapter, 45, 8, null, null);
        await InsertRawAsync(c, t, userId, NoteKinds.Verses, 45, 8, 45008028, 45008030);
        await InsertRawAsync(c, t, userId, NoteKinds.Verse, 45, 8, Rom828, Rom828);
        await InsertRawAsync(c, t, userId, NoteKinds.Words, 45, 8, Rom828, Rom828, 3, 3, "Gott");
        await InsertRawAsync(c, t, userId, NoteKinds.Chapter, 45, 8, null, null, visibility: NoteVisibilities.Public, publicState: NotePublicStates.Pending);

        (await c.ExecuteScalarAsync<int>("""SELECT count(*) FROM "unshackled-word"."Notes" WHERE "UserId" = @UserId""", new { UserId = userId }, t)).ShouldBe(5);
    });

    [Fact]
    public async Task ListChapterAsync_ShouldShowPrivateNotesOnlyToTheAuthor_WhenAnotherUserOrAnonymousAsks() => await WithTransactionAsync(async (c, t) =>
    {
        var author = await NewUserAsync(c, t);
        var other = await NewUserAsync(c, t);
        var verseNote = await NoteRepository.InsertAsync(c, t, author, VerseAnchor, "Vers");
        var chapterNote = await NoteRepository.InsertAsync(c, t, author, ChapterAnchor, "Kapitel");

        var own = await NoteRepository.ListChapterAsync(c, t, 45, 8, author, true);
        own.Where(n => n.UserId == author).Select(n => n.Id).ShouldBe([chapterNote, verseNote]); // reading order: chapter first
        own.Single(n => n.Id == verseNote).BodyMarkdown.ShouldBe("Vers");

        (await NoteRepository.ListChapterAsync(c, t, 45, 8, other, true)).ShouldNotContain(n => n.UserId == author);
        (await NoteRepository.ListChapterAsync(c, t, 45, 8, null, true)).ShouldNotContain(n => n.UserId == author);
        (await NoteRepository.GetAsync(c, t, verseNote, other)).ShouldBeNull();
        (await NoteRepository.GetAsync(c, t, verseNote, null)).ShouldBeNull();
        (await NoteRepository.GetAsync(c, t, verseNote, author)).ShouldNotBeNull().AuthorDisplayName.ShouldStartWith("notes-");
    });

    [Fact]
    public async Task ListChapterAsync_ShouldApplyThePredicate_WhenNotesHaveEveryVisibility() => await WithTransactionAsync(async (c, t) =>
    {
        var author = await NewUserAsync(c, t);
        var other = await NewUserAsync(c, t);
        var @private = await InsertRawAsync(c, t, author, NoteKinds.Chapter, 45, 8, null, null);
        var @internal = await InsertRawAsync(c, t, author, NoteKinds.Chapter, 45, 8, null, null, visibility: NoteVisibilities.Internal);
        var pending = await InsertRawAsync(c, t, author, NoteKinds.Chapter, 45, 8, null, null, visibility: NoteVisibilities.Public, publicState: NotePublicStates.Pending);
        var approved = await InsertRawAsync(c, t, author, NoteKinds.Chapter, 45, 8, null, null, visibility: NoteVisibilities.Public,
            publicState: NotePublicStates.Approved, body: "neu", approvedBody: "freigegeben");
        var hidden = await InsertRawAsync(c, t, author, NoteKinds.Chapter, 45, 8, null, null, visibility: NoteVisibilities.Internal, hidden: true);

        async Task<List<Guid>> SeenBy(int? viewer, bool others = true) =>
            (await NoteRepository.ListChapterAsync(c, t, 45, 8, viewer, others)).Where(n => n.UserId == author).Select(n => n.Id).ToList();

        (await SeenBy(author)).ShouldBe([@private, @internal, pending, approved, hidden], ignoreOrder: true);
        (await SeenBy(other)).ShouldBe([@internal, pending, approved], ignoreOrder: true);
        (await SeenBy(null)).ShouldBe([approved]);
        (await SeenBy(other, others: false)).ShouldBeEmpty();

        (await NoteRepository.GetAsync(c, t, approved, null)).ShouldNotBeNull().BodyMarkdown.ShouldBe("freigegeben");
        (await NoteRepository.GetAsync(c, t, approved, other)).ShouldNotBeNull().BodyMarkdown.ShouldBe("neu");
        (await NoteRepository.GetAsync(c, t, hidden, author)).ShouldNotBeNull().IsHidden.ShouldBeTrue();
    });

    [Fact]
    public async Task UpdateAndDelete_ShouldOnlyTouchTheAuthorsNote_WhenAnotherUserTries() => await WithTransactionAsync(async (c, t) =>
    {
        var author = await NewUserAsync(c, t);
        var other = await NewUserAsync(c, t);
        var id = await NoteRepository.InsertAsync(c, t, author, VerseAnchor, "alt");

        (await NoteRepository.UpdateAsync(c, t, id, other, null, "fremd")).ShouldBeFalse();
        (await NoteRepository.DeleteAsync(c, t, id, other)).ShouldBeFalse();
        (await NoteRepository.UpdateAsync(c, t, id, author, ChapterAnchor, "neu")).ShouldBeTrue();

        var row = (await NoteRepository.GetAsync(c, t, id, author)).ShouldNotBeNull();
        row.BodyMarkdown.ShouldBe("neu");
        row.Kind.ShouldBe(NoteKinds.Chapter);
        row.StartHebRefId.ShouldBeNull();
        row.UpdatedAt.ShouldNotBeNull();

        (await NoteRepository.DeleteAsync(c, t, id, author)).ShouldBeTrue();
        (await NoteRepository.GetAsync(c, t, id, author)).ShouldBeNull();
    });

    [Fact]
    public async Task UpdateAsync_ShouldSendAnApprovedNoteBackToReview_WhenTheAuthorEditsIt() => await WithTransactionAsync(async (c, t) =>
    {
        var author = await NewUserAsync(c, t);
        var id = await InsertRawAsync(c, t, author, NoteKinds.Chapter, 45, 8, null, null, visibility: NoteVisibilities.Public,
            publicState: NotePublicStates.Approved, body: "alt", approvedBody: "alt");

        await NoteRepository.UpdateAsync(c, t, id, author, null, "neu");

        (await NoteRepository.GetAsync(c, t, id, author)).ShouldNotBeNull().PublicState.ShouldBe(NotePublicStates.Pending);
        (await NoteRepository.GetAsync(c, t, id, null)).ShouldNotBeNull().BodyMarkdown.ShouldBe("alt");
    });

    [Theory]
    [InlineData(45008028, true)]
    [InlineData(45008040, false)]
    public async Task VerseExistsAsync_ShouldReflectTheElb1871Text_WhenAskedForRoemer8(int hebRefId, bool exists) => await WithTransactionAsync(async (c, t) =>
        (await NoteRepository.VerseExistsAsync(c, t, hebRefId)).ShouldBe(exists));

    [Theory]
    [InlineData(45, 16, true)]
    [InlineData(45, 17, false)]
    public async Task ChapterExistsAsync_ShouldReflectTheElb1871Text_WhenAskedForRoemer(int book, int chapter, bool exists) => await WithTransactionAsync(async (c, t) =>
        (await NoteRepository.ChapterExistsAsync(c, t, book, chapter)).ShouldBe(exists));
}
