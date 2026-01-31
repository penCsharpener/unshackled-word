using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Abstractions.Step;
using UnshackledWord.Domain.Models.Dbo.Step;

namespace UnshackledWord.Infrastructure.Repositories.Step;

public sealed class StepPersonPlaceRepository : IStepPersonPlaceRepository
{
    private readonly IDbWriter _dbWriter;
    private readonly IDbReader _dbReader;

    public StepPersonPlaceRepository(IDbWriter dbWriter, IDbReader dbReader)
    {
        _dbWriter = dbWriter;
        _dbReader = dbReader;
    }

    public async Task<int> CountPersonsByFilterAsync(StepPersonLexiconFilter filter, CancellationToken token = default)
    {
        var sql = $"""
                   SELECT COUNT(*)
                   FROM {StepPersonLexiconDbo.DbName} AS w
                   WHERE 1=1
                   """;

        return await _dbReader.ExecuteScalarAsync<int>(sql, filter);
    }

    public async Task<int> CountPersonRelationsByFilterAsync(StepPersonLexiconFilter filter, CancellationToken token = default)
    {
        var sql = $"""
                   SELECT COUNT(*)
                   FROM {StepPersonLexiconRelationsDbo.DbName} AS w
                   WHERE 1=1
                   """;

        return await _dbReader.ExecuteScalarAsync<int>(sql, filter);
    }

    public async Task<int> CountPlacesByFilterAsync(StepPersonLexiconFilter filter, CancellationToken token = default)
    {
        var sql = $"""
                   SELECT COUNT(*)
                   FROM {StepPlaceLexiconDbo.DbName} AS w
                   WHERE 1=1
                   """;

        return await _dbReader.ExecuteScalarAsync<int>(sql, filter);
    }

    public async Task<int> CountOthersByFilterAsync(StepPersonLexiconFilter filter, CancellationToken token = default)
    {
        var sql = $"""
                   SELECT COUNT(*)
                   FROM {StepOtherLexiconDbo.DbName} AS w
                   WHERE 1=1
                   """;

        return await _dbReader.ExecuteScalarAsync<int>(sql, filter);
    }

    public async Task BulkInsertAsync(StepPersonLexiconDbo[] entries, CancellationToken token = default)
    {
        if (entries.Length == 0)
        {
            return;
        }

        var valueList = new ColumnInsertCollection();

        foreach (var entry in entries)
        {
            valueList.AddInt(entry.Id);
            valueList.AddString(entry.Name);
            valueList.AddInt(entry.BibleBookId);
            valueList.AddInt(entry.Chapter);
            valueList.AddInt(entry.Verse);
            valueList.AddString(entry.Strongs);
            valueList.AddString(entry.Note);
            valueList.AddString(entry.OriginalSpelling);
            valueList.AddString(entry.Tribe);
            valueList.AddString(entry.Gender);
            valueList.AddString(entry.Briefest);
            valueList.AddString(entry.Brief);
            valueList.AddString(entry.Short);
            valueList.AddString(entry.Article);

            valueList.ValuesToInsertRow();
            valueList.Clear();
        }

        var sql = $"""
                   INSERT INTO {StepPersonLexiconDbo.DbName} (
                       {valueList.GetColumnNames()}
                   ) VALUES
                   {valueList.GetAllInsertRows()}
                   """;

        var parameter = new { };

        await _dbWriter.WriteAsync(sql, parameter);
    }

    public async Task BulkInsertAsync(StepPersonLexiconRelationsDbo[] entries, CancellationToken token = default)
    {
        if (entries.Length == 0)
        {
            return;
        }

        var valueList = new ColumnInsertCollection();

        foreach (var entry in entries)
        {
            valueList.AddInt(entry.Id);
            valueList.AddString(entry.Name);
            valueList.AddInt(entry.PersonLexiconId);
            valueList.AddInt(entry.BibleBookId);
            valueList.AddInt(entry.Chapter);
            valueList.AddInt(entry.Verse);
            valueList.AddString(entry.Strongs);
            valueList.AddString(entry.RelationType);

            valueList.ValuesToInsertRow();
            valueList.Clear();
        }

        var sql = $"""
                   INSERT INTO {StepPersonLexiconRelationsDbo.DbName} (
                       {valueList.GetColumnNames()}
                   ) VALUES
                   {valueList.GetAllInsertRows()}
                   """;

        var parameter = new { };

        await _dbWriter.WriteAsync(sql, parameter);
    }

    public async Task BulkInsertAsync(StepPlaceLexiconDbo[] entries, CancellationToken token = default)
    {
        if (entries.Length == 0)
        {
            return;
        }

        var valueList = new ColumnInsertCollection();

        foreach (var entry in entries)
        {
            entry.Short ??= "";
            entry.Article ??= "";

            valueList.AddInt(entry.Id);
            valueList.AddString(entry.Name);
            valueList.AddInt(entry.BibleBookId);
            valueList.AddInt(entry.Chapter);
            valueList.AddInt(entry.Verse);
            valueList.AddString(entry.Strongs);
            valueList.AddString(entry.Note);
            valueList.AddString(entry.Type);
            valueList.AddString(entry.GoogleMapsLinks);
            valueList.AddString(entry.PalOpenMapsLink);
            valueList.AddString(entry.OriginalSpelling);
            valueList.AddString(entry.StepBibleLink);
            valueList.AddString(entry.Briefest);
            valueList.AddString(entry.Brief);
            valueList.AddString(entry.Short);
            valueList.AddString(entry.Article);

            valueList.ValuesToInsertRow();
            valueList.Clear();
        }

        var sql = $"""
                   INSERT INTO {StepPlaceLexiconDbo.DbName} (
                       {valueList.GetColumnNames()}
                   ) VALUES
                   {valueList.GetAllInsertRows()}
                   """;

        var parameter = new { };

        await _dbWriter.WriteAsync(sql, parameter);
    }

    public async Task BulkInsertAsync(StepOtherLexiconDbo[] entries, CancellationToken token = default)
    {
        if (entries.Length == 0)
        {
            return;
        }

        var valueList = new ColumnInsertCollection();

        foreach (var entry in entries)
        {
            valueList.AddInt(entry.Id);
            valueList.AddString(entry.Name);
            valueList.AddInt(entry.BibleBookId);
            valueList.AddInt(entry.Chapter);
            valueList.AddInt(entry.Verse);
            valueList.AddString(entry.Strongs);
            valueList.AddString(entry.Note);
            valueList.AddString(entry.Type);
            valueList.AddString(entry.OriginalSpelling);
            valueList.AddString(entry.StepBibleLink);
            valueList.AddString(entry.Briefest);
            valueList.AddString(entry.Brief);
            valueList.AddString(entry.Short);
            valueList.AddString(entry.Article);

            valueList.ValuesToInsertRow();
            valueList.Clear();
        }

        var sql = $"""
                   INSERT INTO {StepOtherLexiconDbo.DbName} (
                       {valueList.GetColumnNames()}
                   ) VALUES
                   {valueList.GetAllInsertRows()}
                   """;

        var parameter = new { };

        await _dbWriter.WriteAsync(sql, parameter);
    }
}
