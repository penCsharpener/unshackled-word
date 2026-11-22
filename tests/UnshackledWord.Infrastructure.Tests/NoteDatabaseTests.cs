using System.Data;
using Dapper;
using Npgsql;
using UnshackledWord.Application.Features.Notes;
using UnshackledWord.Domain.WebApi.Notes;
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

    private static NoteAnchor WordsAnchor(int from, int to, string quote) => new(NoteKinds.Words, 45, 8, Rom828, Rom828, from, to, quote);

    /// <summary>The read path of <see cref="NoteService"/> on a connection in the test transaction.</summary>
    private static async Task<NoteResponse> ReadAsync(IDbConnection c, IDbTransaction t, Guid id, int userId, int? level = null)
    {
        var row = (await NoteRepository.GetAsync(c, t, id, userId)).ShouldNotBeNull();
        var response = NoteService.ToResponse(row, userId);
        var elb = (await NoteRepository.GetElbWordsAsync(c, t, [Rom828])).ToList();
        var variant = level is > 0 ? (await NoteRepository.GetVariantWordsAsync(c, t, [Rom828], level.Value)).ToList() : null;
        NoteService.ApplyWordsState(response, row, elb, variant);

        return response;
    }

    private static async Task<string> QuoteAsync(IDbConnection c, IDbTransaction t, int from, int to) =>
        NoteQuote.Join((await NoteRepository.GetElbWordsAsync(c, t, [Rom828])).Where(w => w.PositionInVerse >= from && w.PositionInVerse <= to).Select(w => w.WordInContext));

    [Fact]
    public async Task WordsNote_ShouldBeOk_WhenTheElb1871WordsStillEqualTheQuote() => await WithTransactionAsync(async (c, t) =>
    {
        var author = await NewUserAsync(c, t);
        var quote = await QuoteAsync(c, t, 3, 5);
        quote.ShouldNotBeNullOrWhiteSpace();
        var id = await NoteRepository.InsertAsync(c, t, author, WordsAnchor(3, 5, quote), "Wörter");

        var response = await ReadAsync(c, t, id, author);

        response.AnchorState.ShouldBe(NoteAnchorStates.Ok);
        response.Anchor.QuotedText.ShouldBe(quote);
        response.MappedWords.ShouldBeNull();
    });

    [Fact]
    public async Task WordsNote_ShouldBeDrifted_WhenAWordInContextChangesAfterTheNoteWasSaved() => await WithTransactionAsync(async (c, t) =>
    {
        var author = await NewUserAsync(c, t);
        var quote = await QuoteAsync(c, t, 3, 5);
        var id = await NoteRepository.InsertAsync(c, t, author, WordsAnchor(3, 5, quote), "Wörter");

        await c.ExecuteAsync("""
            UPDATE "unshackled-word"."Elb1871Words"
            SET "WordInContext" = 'ersetzt'
            WHERE "HebRefId" = @HebRefId AND "PositionInVerse" = 4
            """, new { HebRefId = Rom828 }, t);

        var response = await ReadAsync(c, t, id, author);

        response.AnchorState.ShouldBe(NoteAnchorStates.Drifted);
        response.Anchor.QuotedText.ShouldBe(quote);
    });

    [Fact]
    public async Task WordsNote_ShouldBeDrifted_WhenThePositionsAreGone() => await WithTransactionAsync(async (c, t) =>
    {
        var author = await NewUserAsync(c, t);
        var id = await NoteRepository.InsertAsync(c, t, author, WordsAnchor(900, 901, "weg"), "Wörter");

        (await ReadAsync(c, t, id, author)).AnchorState.ShouldBe(NoteAnchorStates.Drifted);
    });

    [Fact]
    public async Task WordsNote_ShouldMapThroughTheVariant_WhenTheVerseIsRevisedAtTheLevel() => await WithTransactionAsync(async (c, t) =>
    {
        var author = await NewUserAsync(c, t);
        var elb = (await NoteRepository.GetElbWordsAsync(c, t, [Rom828])).ToList();
        elb.Count.ShouldBeGreaterThan(5);
        var quote = await QuoteAsync(c, t, 3, 4);
        var id = await NoteRepository.InsertAsync(c, t, author, WordsAnchor(3, 4, quote), "Wörter");

        // a test revision above every existing level (it wins at its own level); its variant of Röm 8,28: base 1,2 same, one new word, base 3,4 behind it, base 5 dropped
        var level = await c.QuerySingleAsync<int>("""SELECT COALESCE(MAX("Level") FILTER (WHERE "Level" < 1000), 200) + 1 FROM "unshackled-word"."Revisions" """, transaction: t);
        var revisionId = await c.QuerySingleAsync<int>("""
            INSERT INTO "unshackled-word"."Revisions" ("Level", "Code", "NameDe", "NameEn")
            VALUES (@Level, 'notes-test', 'Notizen-Test', 'Notes test')
            RETURNING "Id"
            """, new { Level = level }, t);
        var verseId = await c.QuerySingleAsync<int>("""
            INSERT INTO "unshackled-word"."RevisionVerses" ("HebRefId", "RevisionId", "Version", "VerseText", "BaseTextHash")
            VALUES (@HebRefId, @RevisionId, 1, 'x', 'x')
            RETURNING "Id"
            """, new { HebRefId = Rom828, RevisionId = revisionId }, t);
        var variantWords = new (int Position, int? Base, string Change)[] { (1, 1, "same"), (2, 2, "same"), (3, null, "new"), (4, 3, "changed"), (5, 4, "same") };
        foreach (var (position, basePosition, change) in variantWords)
        {
            await c.ExecuteAsync("""
                INSERT INTO "unshackled-word"."RevisionWords" ("RevisionVerseId", "PositionInVerse", "WordInContext", "BasePositionInVerse", "Change")
                VALUES (@VerseId, @Position, 'w', @Base, @Change)
                """, new { VerseId = verseId, Position = position, Base = basePosition, Change = change }, t);
        }

        var response = await ReadAsync(c, t, id, author, level);

        response.AnchorState.ShouldBe(NoteAnchorStates.Ok);
        response.RevisionVerseId.ShouldBe(verseId);
        response.MappedWords.ShouldNotBeNull().Select(w => (w.PositionInVerse, w.BasePositionInVerse)).ShouldBe([(4, 3), (5, 4)]);

        // a note on a base word the variant dropped has nothing to mark
        var droppedQuote = await QuoteAsync(c, t, 6, 6);
        var dropped = await NoteRepository.InsertAsync(c, t, author, WordsAnchor(6, 6, droppedQuote), "weg");
        (await ReadAsync(c, t, dropped, author, level)).AnchorState.ShouldBe(NoteAnchorStates.NoBaseWords);

        // without a level the Elb1871 positions apply
        (await ReadAsync(c, t, id, author)).MappedWords.ShouldBeNull();
    });

    private static Task<Guid> InsertAtAsync(IDbConnection c, IDbTransaction t, int userId, int book, string body, DateTime changedAt, string visibility = NoteVisibilities.Private) =>
        c.QuerySingleAsync<Guid>("""
            INSERT INTO "unshackled-word"."Notes" ("UserId", "Kind", "BibleBookId", "Chapter", "BodyMarkdown", "Visibility", "PublicState", "CreatedAt", "UpdatedAt")
            VALUES (@UserId, 'chapter', @Book, 1, @Body, @Visibility, CASE WHEN @Visibility = 'public' THEN 'pending' ELSE 'none' END, @ChangedAt, @ChangedAt)
            RETURNING "Id"
            """, new { UserId = userId, Book = book, Body = body, Visibility = visibility, ChangedAt = changedAt }, t);

    [Fact]
    public async Task ListMineAsync_ShouldPageNewestChangeFirstAndReportTheTotal_WhenTheUserHas120Notes() => await WithTransactionAsync(async (c, t) =>
    {
        var author = await NewUserAsync(c, t);
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var ids = new List<Guid>();
        for (var i = 0; i < 120; i++)
        {
            ids.Add(await InsertAtAsync(c, t, author, 45, $"Notiz {i}", start.AddMinutes(i)));
        }

        var first = await NoteRepository.ListMineAsync(c, t, author, new NoteListFilter(null, null, null, 0, 50));
        var second = await NoteRepository.ListMineAsync(c, t, author, new NoteListFilter(null, null, null, 50, 50));
        var third = await NoteRepository.ListMineAsync(c, t, author, new NoteListFilter(null, null, null, 100, 50));

        first.Total.ShouldBe(120);
        first.Rows.Count.ShouldBe(50);
        second.Rows.Count.ShouldBe(50);
        third.Rows.Count.ShouldBe(20);
        first.Rows.Select(n => n.Id).Concat(second.Rows.Select(n => n.Id)).Concat(third.Rows.Select(n => n.Id)).ShouldBe(Enumerable.Reverse(ids));
    });

    [Fact]
    public async Task ListMineAsync_ShouldOrderByCreatedAt_WhenTheNoteWasNeverEdited() => await WithTransactionAsync(async (c, t) =>
    {
        var author = await NewUserAsync(c, t);
        var old = await InsertAtAsync(c, t, author, 45, "alt", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var neverEdited = await NoteRepository.InsertAsync(c, t, author, ChapterAnchor, "frisch"); // UpdatedAt null, CreatedAt now
        await c.ExecuteAsync("""UPDATE "unshackled-word"."Notes" SET "UpdatedAt" = NULL WHERE "Id" = @Id""", new { Id = old }, t);

        var page = await NoteRepository.ListMineAsync(c, t, author, new NoteListFilter(null, null, null, 0, 50));

        page.Rows.Select(n => n.Id).ShouldBe([neverEdited, old]);
    });

    [Fact]
    public async Task ListMineAsync_ShouldFilterByBookAndVisibility_WhenBothAreGiven() => await WithTransactionAsync(async (c, t) =>
    {
        var author = await NewUserAsync(c, t);
        var at = DateTime.UtcNow;
        var romPrivate = await InsertAtAsync(c, t, author, 45, "a", at);
        var romInternal = await InsertAtAsync(c, t, author, 45, "b", at.AddMinutes(1), NoteVisibilities.Internal);
        var johnPrivate = await InsertAtAsync(c, t, author, 43, "c", at.AddMinutes(2));

        (await NoteRepository.ListMineAsync(c, t, author, new NoteListFilter(45, null, null, 0, 50))).Rows.Select(n => n.Id).ShouldBe([romInternal, romPrivate]);
        (await NoteRepository.ListMineAsync(c, t, author, new NoteListFilter(null, NoteVisibilities.Private, null, 0, 50))).Rows.Select(n => n.Id).ShouldBe([johnPrivate, romPrivate]);
        var both = await NoteRepository.ListMineAsync(c, t, author, new NoteListFilter(45, NoteVisibilities.Internal, null, 0, 50));
        both.Total.ShouldBe(1);
        both.Rows.Single().Id.ShouldBe(romInternal);
    });

    [Theory]
    [InlineData("GOTT", 1)]
    [InlineData("100%", 1)]
    [InlineData("%", 1)]
    [InlineData("a_b", 1)]
    [InlineData("_", 1)]
    [InlineData("\\", 1)]
    [InlineData("nichts", 0)]
    public async Task ListMineAsync_ShouldSearchTheBodyCaseInsensitiveWithLiteralWildcards_WhenQIsGiven(string q, int expected) => await WithTransactionAsync(async (c, t) =>
    {
        var author = await NewUserAsync(c, t);
        var at = DateTime.UtcNow;
        await InsertAtAsync(c, t, author, 45, "Gott ist treu", at);
        await InsertAtAsync(c, t, author, 45, "xaxb", at.AddMinutes(1)); // would match 'a_b' and '%' if they were wildcards
        // exactly one of these bodies contains each special character as text
        await c.ExecuteAsync("""UPDATE "unshackled-word"."Notes" SET "BodyMarkdown" = 'Gott 100% treu a_b \\ ende' WHERE "UserId" = @UserId AND "BodyMarkdown" = 'Gott ist treu'""", new { UserId = author }, t);

        var page = await NoteRepository.ListMineAsync(c, t, author, new NoteListFilter(null, null, q, 0, 50));

        page.Total.ShouldBe(expected);
        page.Rows.Count.ShouldBe(expected);
    });

    [Fact]
    public async Task ListMineAsync_ShouldNeverReturnAnotherUsersNotes_WhenTheyAreInternalOrPublic() => await WithTransactionAsync(async (c, t) =>
    {
        var me = await NewUserAsync(c, t);
        var other = await NewUserAsync(c, t);
        var mine = await InsertAtAsync(c, t, me, 45, "Gott meins", DateTime.UtcNow);
        await InsertAtAsync(c, t, other, 45, "Gott fremd intern", DateTime.UtcNow, NoteVisibilities.Internal);
        await InsertAtAsync(c, t, other, 45, "Gott fremd öffentlich", DateTime.UtcNow, NoteVisibilities.Public);

        var all = await NoteRepository.ListMineAsync(c, t, me, new NoteListFilter(null, null, null, 0, 50));
        var search = await NoteRepository.ListMineAsync(c, t, me, new NoteListFilter(45, NoteVisibilities.Public, "Gott", 0, 50));

        all.Total.ShouldBe(1);
        all.Rows.Single().Id.ShouldBe(mine);
        search.Total.ShouldBe(0);
        (await NoteRepository.ListForExportAsync(c, t, me)).Select(n => n.Id).ShouldBe([mine]);
    });

    [Fact]
    public async Task ListForExportAsync_ShouldReturnEveryOwnNoteWithItsAnchorAndApprover_WhenTheUserHasAllKinds() => await WithTransactionAsync(async (c, t) =>
    {
        var author = await NewUserAsync(c, t);
        var approver = await NewUserAsync(c, t, "approver");
        var quote = await QuoteAsync(c, t, 3, 5);
        var chapter = await NoteRepository.InsertAsync(c, t, author, ChapterAnchor, "Kapitel");
        var verse = await NoteRepository.InsertAsync(c, t, author, VerseAnchor, "Vers");
        var range = await NoteRepository.InsertAsync(c, t, author, new NoteAnchor(NoteKinds.Verses, 45, 8, 45008028, 45008030, null, null, null), "Bereich");
        var words = await NoteRepository.InsertAsync(c, t, author, WordsAnchor(3, 5, quote), "Wörter");
        await c.ExecuteAsync("""
            UPDATE "unshackled-word"."Notes"
            SET "Visibility" = 'public', "PublicState" = 'approved', "ApprovedBodyMarkdown" = 'Vers', "ApprovedAt" = now(), "ApprovedByUserId" = @Approver
            WHERE "Id" = @Id
            """, new { Id = verse, Approver = approver }, t);

        var rows = await NoteRepository.ListForExportAsync(c, t, author);

        rows.Count.ShouldBe(4);
        rows.Select(r => r.Id).ShouldBe(new[] { chapter, verse, range, words }.Order());
        var wordsRow = rows.Single(r => r.Id == words);
        (wordsRow.StartPosition, wordsRow.EndPosition, wordsRow.QuotedText, wordsRow.StartHebRefId).ShouldBe((3, 5, quote, Rom828));
        var range2 = rows.Single(r => r.Id == range);
        (range2.StartHebRefId, range2.EndHebRefId).ShouldBe((45008028, 45008030));
        var verseRow = rows.Single(r => r.Id == verse);
        verseRow.ApprovedByEmail.ShouldStartWith("approver-");
        verseRow.AuthorEmail.ShouldStartWith("notes-");
        verseRow.ApprovedBodyMarkdown.ShouldBe("Vers");
        rows.Single(r => r.Id == chapter).StartHebRefId.ShouldBeNull();
    });
}
