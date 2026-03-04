using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Models.Dbo.Step;
using UnshackledWord.Infrastructure.Repositories;
using UnshackledWord.Tooling.SeedDb.Services.Abstractions;

namespace UnshackledWord.Tooling.SeedDb.Services.StepBible;

public sealed class StepBibleStructureStrategy : IFileParserStrategy
{
    private readonly IDbReader _dbReader;
    private readonly IDbWriter _dbWriter;
    private readonly ILogger<StepBibleStructureStrategy> _logger;

    public StepBibleStructureStrategy(IDbReader dbReader,
        IDbWriter dbWriter,
        ILogger<StepBibleStructureStrategy> logger)
    {
        _dbReader = dbReader;
        _dbWriter = dbWriter;
        _logger = logger;
    }

    public async Task SaveToDatabase(string _, CancellationToken token = default)
    {
        await PopulateLastVerseTableAsync();
        await PopulateLastChapterTableAsync();
    }

    private async Task PopulateLastChapterTableAsync()
    {
        var sqlCount = $"""
                        SELECT COUNT(*) FROM {BibleStructureChaptersDbo.DbName};
                        """;

        var existingCount = await _dbReader.ExecuteScalarAsync<int>(sqlCount);
        if (existingCount > 0)
        {
            _logger.LogInformation("Step Bible structure already imported...");
            return;
        }

        var sqlSource = """
                        WITH StandardChapters AS (
                            -- Get the maximum standard chapter for each BibleBookId
                            SELECT "BibleBookId", MAX("Chapter") AS MaxChapter
                            FROM (
                                SELECT "BibleBookId", "Chapter" FROM "unshackled-word"."StepHebrewWords"
                                UNION ALL
                                SELECT "BibleBookId", "Chapter" FROM "unshackled-word"."StepGreekWords"
                            ) std
                            GROUP BY "BibleBookId"
                        ),
                        AltChapters AS (
                            -- Get the maximum alternative chapter for each BibleBookId
                            SELECT "BibleBookId", MAX("AltChapter") AS MaxAltChapter
                            FROM (
                                SELECT "BibleBookId", "AltChapter" FROM "unshackled-word"."StepHebrewWords"
                                WHERE "AltChapter" IS NOT NULL
                                UNION ALL
                                SELECT "BibleBookId", "AltChapter" FROM "unshackled-word"."StepGreekWords"
                                WHERE "AltChapter" IS NOT NULL
                            ) alt
                            GROUP BY "BibleBookId"
                        )
                        SELECT
                            COALESCE(s."BibleBookId", a."BibleBookId") AS "BibleBookId",
                            s.MaxChapter AS "LastChapter",
                            a.MaxAltChapter AS "AltLastChapter"
                        FROM StandardChapters s
                        FULL OUTER JOIN AltChapters a
                            ON s."BibleBookId" = a."BibleBookId"
                        ORDER BY 1;
                        """;

        var sqlVerses = await _dbReader.ReadAsListAsync<BibleStructureChaptersDbo>(sqlSource);

        foreach (var chapter in sqlVerses)
        {
            var valueList = new ColumnInsertCollection();

            valueList.AddInt(chapter.BibleBookId);
            valueList.AddInt(chapter.LastChapter);

            if (chapter.BibleBookId == 29 || chapter.BibleBookId == 39)
            {
                valueList.AddInt(chapter.AltLastChapter);
            }

            valueList.ValuesToInsertRow();
            valueList.Clear();

            var sql = $"""
                       INSERT INTO {BibleStructureChaptersDbo.DbName} (
                           {valueList.GetColumnNames()}
                       ) VALUES
                       {valueList.GetAllInsertRows()}
                       """;

            var parameter = new { };

            await _dbWriter.WriteAsync(sql, parameter);
        }
    }

    private async Task PopulateLastVerseTableAsync()
    {
        var sqlCount = $"""
                        SELECT COUNT(*) FROM {BibleStructureVersesDbo.DbName};
                        """;

        var existingCount = await _dbReader.ExecuteScalarAsync<int>(sqlCount);
        if (existingCount > 0)
        {
            return;
        }

        var sqlSource = """
                        WITH StandardVerses AS (
                            -- Aggregate standard BibleBookId/Chapter max verses
                            SELECT "BibleBookId", "Chapter", MAX("Verse") AS MaxVerse
                            FROM (
                                SELECT "BibleBookId", "Chapter", "Verse" FROM "unshackled-word"."StepHebrewWords"
                                UNION ALL
                                SELECT "BibleBookId", "Chapter", "Verse" FROM "unshackled-word"."StepGreekWords"
                            ) std
                            GROUP BY "BibleBookId", "Chapter"
                        ),
                        AltVerses AS (
                            -- Aggregate alternative BibleBookId/AltChapter max verses
                            SELECT "BibleBookId", "AltChapter", MAX("AltVerse") AS MaxAltVerse
                            FROM (
                                SELECT "BibleBookId", "AltChapter", "AltVerse" FROM "unshackled-word"."StepHebrewWords"
                                WHERE "AltChapter" IS NOT NULL
                                UNION ALL
                                SELECT "BibleBookId", "AltChapter", "AltVerse" FROM "unshackled-word"."StepGreekWords"
                                WHERE "AltChapter" IS NOT NULL
                            ) alt
                            GROUP BY "BibleBookId", "AltChapter"
                        )
                        SELECT
                            COALESCE(s."BibleBookId", a."BibleBookId") AS "BibleBookId",
                            s."Chapter",
                            s.MaxVerse AS "LastVerse",
                            a."AltChapter" AS "AltChapter",
                            a.MaxAltVerse AS "AltLastVerse"
                        FROM StandardVerses s
                        FULL OUTER JOIN AltVerses a
                            ON s."BibleBookId" = a."BibleBookId"
                            AND s."Chapter" = a."AltChapter"
                        ORDER BY 1, 2;
                        """;

        var sqlVerses = await _dbReader.ReadAsListAsync<BibleStructureVersesDbo>(sqlSource);

        foreach (var chapter in sqlVerses)
        {
            var valueList = new ColumnInsertCollection();

            valueList.AddInt(chapter.BibleBookId);
            valueList.AddInt(chapter.Chapter);
            valueList.AddInt(chapter.LastVerse);
            valueList.AddInt(chapter.AltChapter);
            valueList.AddInt(chapter.AltLastVerse);

            valueList.ValuesToInsertRow();
            valueList.Clear();

            var sql = $"""
                       INSERT INTO {BibleStructureVersesDbo.DbName} (
                           {valueList.GetColumnNames()}
                       ) VALUES
                       {valueList.GetAllInsertRows()}
                       """;

            var parameter = new { };

            await _dbWriter.WriteAsync(sql, parameter);
        }
    }
}
