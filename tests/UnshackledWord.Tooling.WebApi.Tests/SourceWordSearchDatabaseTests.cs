using Dapper;
using Microsoft.Extensions.DependencyInjection;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Tooling.WebApi.Endpoints.DataQuality;
using UnshackledWord.Tooling.WebApi.Endpoints.Elberfelder.Search.SourceWords;

namespace UnshackledWord.Tooling.WebApi.Tests;

/// <summary>
/// The SQL of the source word search (form index, verse refs) against a real Postgres. Set <c>UNSHACKLEDWORD_TEST_POSTGRES</c> to an EMPTY throwaway database:
/// the test creates the schema "unshackled-word" with the two tables it reads and drops it again. It is skipped when the variable is missing
/// or when the schema already exists (a copy of the dev database is never touched).
/// </summary>
public class SourceWordSearchDatabaseTests
{
    private const string Seed = """
        CREATE SCHEMA "unshackled-word";
        CREATE TABLE "unshackled-word"."DqWordFacts"
        (
            "Lang"         char(1),
            "ElbWordId"    int,
            "StepWordId"   int,
            "BibleBookId"  int,
            "Chapter"      int,
            "Verse"        int,
            "SourceWord"   varchar,
            "StrongsBase"  varchar
        );
        CREATE TABLE "unshackled-word"."StepStrongsLexicon"
        (
            "LanguageId"               int,
            "Number"                   int,
            "Extra"                    varchar,
            "OriginalWord"             varchar,
            "OriginalWordNoDiacritics" varchar,
            "Transliteration"          varchar,
            "Gloss"                    varchar
        );
        INSERT INTO "unshackled-word"."StepStrongsLexicon" VALUES (2, 3056, NULL, 'λόγος', 'λογος', 'logos', 'word');
        INSERT INTO "unshackled-word"."StepStrongsLexicon" VALUES (0, 4428, NULL, 'מֶלֶךְ', 'מלך', 'melek', 'king');
        -- λόγος: word 1 twice in John 1:1 (two German words aligned), word 2 in John 1:14; λόγου: word 3 in Mark 1:1; the king: Genesis 14:1
        INSERT INTO "unshackled-word"."DqWordFacts" VALUES ('G', 1, 1, 43, 1, 1, 'λόγος', 'G3056');
        INSERT INTO "unshackled-word"."DqWordFacts" VALUES ('G', 2, 1, 43, 1, 1, 'λόγος', 'G3056');
        INSERT INTO "unshackled-word"."DqWordFacts" VALUES ('G', 3, 2, 43, 1, 14, 'λόγος', 'G3056');
        INSERT INTO "unshackled-word"."DqWordFacts" VALUES ('G', 4, 3, 41, 1, 1, 'λόγου', 'G3056');
        INSERT INTO "unshackled-word"."DqWordFacts" VALUES ('H', 5, 4, 1, 14, 1, 'מֶלֶךְ', 'H4428');
        INSERT INTO "unshackled-word"."DqWordFacts" VALUES ('G', NULL, 5, NULL, NULL, NULL, 'λόγος', 'G3056');
        """;

    private static async Task WithSchemaAsync(Func<SourceWordSearchService, Task> test)
    {
        var connectionString = UserAdminDatabaseTests.ConnectionString;
        Assert.SkipWhen(connectionString is null, $"{UserAdminDatabaseTests.EnvVar} is not set.");

        var factory = new UserAdminDatabaseTests.TestFactory(connectionString!);
        using (var connection = await factory.CreateDbConnectionAsync())
        {
            var exists = await connection.ExecuteScalarAsync<bool>("""SELECT EXISTS (SELECT 1 FROM information_schema.schemata WHERE schema_name = 'unshackled-word')""");
            Assert.SkipWhen(exists, "The schema 'unshackled-word' exists; the test needs an empty throwaway database.");
            await connection.ExecuteAsync(Seed);
        }

        try
        {
            var services = new ServiceCollection();
            services.AddScoped<IDbConnectionFactory>(_ => new UserAdminDatabaseTests.TestFactory(connectionString!));
            await using var provider = services.BuildServiceProvider();
            var scopes = provider.GetRequiredService<IServiceScopeFactory>();
            var service = new SourceWordSearchService(
                new StrongsLexiconIndex(scopes),
                new SourceFormIndex(scopes, TimeProvider.System),
                new SourceWordRefs(new UserAdminDatabaseTests.TestFactory(connectionString!)));
            await test(service);
        }
        finally
        {
            using var connection = await factory.CreateDbConnectionAsync();
            await connection.ExecuteAsync("""DROP SCHEMA "unshackled-word" CASCADE""");
        }
    }

    [Fact]
    public Task Forms_are_counted_per_original_word_and_listed_with_their_first_verses() => WithSchemaAsync(async service =>
    {
        var lemma = (await service.SearchAsync("λογος", 8, default))!.Tokens.Single().Lemmas.Single();

        lemma.Strongs.ShouldBe("G3056");
        lemma.Occurrences.ShouldBe(4);
        var form = lemma.Forms.Single(f => f.Form == "λόγος");
        form.Occurrences.ShouldBe(3);
        form.Refs.Select(r => (r.BibleBookId, r.Chapter, r.Verse)).ShouldBe([(43, 1, 1), (43, 1, 14)]);
    });

    [Fact]
    public Task An_inflected_form_without_diacritics_resolves_to_its_lemma() => WithSchemaAsync(async service =>
    {
        var lemma = (await service.SearchAsync("λογου", 8, default))!.Tokens.Single().Lemmas.First();

        lemma.Strongs.ShouldBe("G3056");
        lemma.Forms.Select(f => f.Form).ShouldContain("λόγου");
    });

    [Fact]
    public Task Hebrew_with_a_final_letter_and_without_points_finds_the_word() => WithSchemaAsync(async service =>
    {
        var lemma = (await service.SearchAsync("מלך", 8, default))!.Tokens.Single().Lemmas.Single();

        lemma.Strongs.ShouldBe("H4428");
        lemma.Forms.Single().Refs.Single().BibleBookId.ShouldBe(1);
    });
}
