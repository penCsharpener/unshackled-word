using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Abstractions.Step;
using UnshackledWord.Domain.Extensions;
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

        var dataSize = entries.Length + 1;
        var parameters = new
        {
            Id = new List<int>(dataSize),
            Name = new List<string>(dataSize),
            LxxRefId = new List<int>(dataSize),
            Strongs = new List<string?>(dataSize),
            Note = new List<string?>(dataSize),
            OriginalSpelling = new List<string?>(dataSize),
            Tribe = new List<string?>(dataSize),
            Gender = new List<string?>(dataSize),
            Briefest = new List<string?>(dataSize),
            Brief = new List<string?>(dataSize),
            Short = new List<string?>(dataSize),
            Article = new List<string?>(dataSize),
        };

        foreach (var entry in entries)
        {
            parameters.Id.Add(entry.Id);
            parameters.Name.Add(entry.Name);
            parameters.LxxRefId.Add(entry.LxxRefId);
            parameters.Strongs.Add(entry.Strongs);
            parameters.Note.Add(entry.Note);
            parameters.OriginalSpelling.Add(entry.OriginalSpelling);
            parameters.Tribe.Add(entry.Tribe);
            parameters.Gender.Add(entry.Gender);
            parameters.Briefest.Add(entry.Briefest);
            parameters.Brief.Add(entry.Brief);
            parameters.Short.Add(entry.Short);
            parameters.Article.Add(entry.Article);
        }

        var names = PropertyListHelper.GetPropertyNames(parameters);

        var sql = $"""
                   INSERT INTO {StepPersonLexiconDbo.DbName} (
                       {names.Select(x => $"\"{x}\"").JoinStrings(",")}
                   )
                   SELECT *
                   FROM UNNEST({names.Select(x => $"@{x}").JoinStrings(",")})
                   ON CONFLICT DO NOTHING;
                   """;

        await _dbWriter.WriteAsync(sql, parameters);
    }

    public async Task BulkInsertAsync(StepPersonLexiconRelationsDbo[] entries, CancellationToken token = default)
    {
        if (entries.Length == 0)
        {
            return;
        }

        var dataSize = entries.Length + 1;
        var parameters = new
        {
            Id = new List<int>(dataSize),
            Name = new List<string>(dataSize),
            PersonLexiconId = new List<int>(dataSize),
            LxxRefId = new List<int>(dataSize),
            Strongs = new List<string?>(dataSize),
            RelationType = new List<string>(dataSize),
        };

        foreach (var entry in entries)
        {
            parameters.Id.Add(entry.Id);
            parameters.Name.Add(entry.Name);
            parameters.PersonLexiconId.Add(entry.PersonLexiconId);
            parameters.LxxRefId.Add(entry.LxxRefId);
            parameters.Strongs.Add(entry.Strongs);
            parameters.RelationType.Add(entry.RelationType);
        }

        var names = PropertyListHelper.GetPropertyNames(parameters);

        var sql = $"""
                   INSERT INTO {StepPersonLexiconRelationsDbo.DbName} (
                       {names.Select(x => $"\"{x}\"").JoinStrings(",")}
                   )
                   SELECT *
                   FROM UNNEST({names.Select(x => $"@{x}").JoinStrings(",")})
                   ON CONFLICT DO NOTHING;
                   """;

        await _dbWriter.WriteAsync(sql, parameters);
    }

    public async Task BulkInsertAsync(StepPlaceLexiconDbo[] entries, CancellationToken token = default)
    {
        if (entries.Length == 0)
        {
            return;
        }

        var dataSize = entries.Length + 1;
        var parameters = new
        {
            Id = new List<int>(dataSize),
            Name = new List<string>(dataSize),
            LxxRefId = new List<int>(dataSize),
            Strongs = new List<string>(dataSize),
            Note = new List<string?>(dataSize),
            Type = new List<string?>(dataSize),
            GoogleMapsLinks = new List<string?>(dataSize),
            PalOpenMapsLink = new List<string?>(dataSize),
            OriginalSpelling = new List<string?>(dataSize),
            StepBibleLink = new List<string>(dataSize),
            Briefest = new List<string?>(dataSize),
            Brief = new List<string?>(dataSize),
            Short = new List<string>(dataSize),
            Article = new List<string>(dataSize),
        };

        foreach (var entry in entries)
        {
            entry.Short ??= "";
            entry.Article ??= "";

            parameters.Id.Add(entry.Id);
            parameters.Name.Add(entry.Name);
            parameters.LxxRefId.Add(entry.LxxRefId);
            parameters.Strongs.Add(entry.Strongs);
            parameters.Note.Add(entry.Note);
            parameters.Type.Add(entry.Type);
            parameters.GoogleMapsLinks.Add(entry.GoogleMapsLinks);
            parameters.PalOpenMapsLink.Add(entry.PalOpenMapsLink);
            parameters.OriginalSpelling.Add(entry.OriginalSpelling);
            parameters.StepBibleLink.Add(entry.StepBibleLink);
            parameters.Briefest.Add(entry.Briefest);
            parameters.Brief.Add(entry.Brief);
            parameters.Short.Add(entry.Short);
            parameters.Article.Add(entry.Article);
        }

        var names = PropertyListHelper.GetPropertyNames(parameters);

        var sql = $"""
                   INSERT INTO {StepPlaceLexiconDbo.DbName} (
                       {names.Select(x => $"\"{x}\"").JoinStrings(",")}
                   )
                   SELECT *
                   FROM UNNEST({names.Select(x => $"@{x}").JoinStrings(",")})
                   ON CONFLICT DO NOTHING;
                   """;

        await _dbWriter.WriteAsync(sql, parameters);
    }

    public async Task BulkInsertAsync(StepOtherLexiconDbo[] entries, CancellationToken token = default)
    {
        if (entries.Length == 0)
        {
            return;
        }

        var dataSize = entries.Length + 1;
        var parameters = new
        {
            Id = new List<int>(dataSize),
            Name = new List<string>(dataSize),
            LxxRefId = new List<int>(dataSize),
            Strongs = new List<string>(dataSize),
            Note = new List<string?>(dataSize),
            Type = new List<string?>(dataSize),
            OriginalSpelling = new List<string?>(dataSize),
            StepBibleLink = new List<string>(dataSize),
            Briefest = new List<string?>(dataSize),
            Brief = new List<string>(dataSize),
            Short = new List<string>(dataSize),
            Article = new List<string>(dataSize),
        };

        foreach (var entry in entries)
        {
            parameters.Id.Add(entry.Id);
            parameters.Name.Add(entry.Name);
            parameters.LxxRefId.Add(entry.LxxRefId);
            parameters.Strongs.Add(entry.Strongs);
            parameters.Note.Add(entry.Note);
            parameters.Type.Add(entry.Type);
            parameters.OriginalSpelling.Add(entry.OriginalSpelling);
            parameters.StepBibleLink.Add(entry.StepBibleLink);
            parameters.Briefest.Add(entry.Briefest);
            parameters.Brief.Add(entry.Brief);
            parameters.Short.Add(entry.Short);
            parameters.Article.Add(entry.Article);
        }

        var names = PropertyListHelper.GetPropertyNames(parameters);

        var sql = $"""
                   INSERT INTO {StepOtherLexiconDbo.DbName} (
                       {names.Select(x => $"\"{x}\"").JoinStrings(",")}
                   )
                   SELECT *
                   FROM UNNEST({names.Select(x => $"@{x}").JoinStrings(",")})
                   ON CONFLICT DO NOTHING;
                   """;

        await _dbWriter.WriteAsync(sql, parameters);
    }
}
