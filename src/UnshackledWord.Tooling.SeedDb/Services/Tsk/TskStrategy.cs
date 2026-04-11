using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Extensions;
using UnshackledWord.Domain.Models.BibleStructure;
using UnshackledWord.Domain.Models.Dbo;
using UnshackledWord.Infrastructure.Repositories;
using UnshackledWord.Tooling.SeedDb.Services.Abstractions;
using UnshackledWord.Tooling.SeedDb.Services.Tsk.Models;

namespace UnshackledWord.Tooling.SeedDb.Services.Tsk;

public class TskStrategy : IFileParserStrategy
{
    private readonly IDbWriter _dbWriter;
    private readonly TskTextReader _tskTextReader;
    private static string delimiter = $",{Environment.NewLine}    ";

    public TskStrategy(IDbWriter dbWriter, TskTextReader tskTextReader)
    {
        _dbWriter = dbWriter;
        _tskTextReader = tskTextReader;
    }

    public async Task SaveToDatabase(string _, CancellationToken token = default)
    {
        var tskReferences = await _tskTextReader.ReadAsync(token);

        await InsertAsync(tskReferences, token);
    }

    public async Task InsertAsync(ICollection<TskReference> tskReferences, CancellationToken token = default)
    {
        var parameters = new
        {
            LxxRefId = new List<int>(),
            Scope = new List<string>(),
            RelatedStartLxxRefId = new List<int>(),
            RelatedEndLxxRefId = new List<int?>(),
        };

        foreach (var tskReference in tskReferences)
        {
            foreach (var crossRef in tskReference.CrossReferences)
            {
                parameters.LxxRefId.Add(tskReference.Reference.RefId);
                parameters.Scope.Add(tskReference.Words);

                if (crossRef is BibleReference bibleRef)
                {
                    parameters.RelatedStartLxxRefId.Add(bibleRef.RefId);
                    parameters.RelatedEndLxxRefId.Add(null);
                }

                if (crossRef is BibleReferenceRange range)
                {
                    parameters.RelatedStartLxxRefId.Add(range.Start.RefId);
                    parameters.RelatedEndLxxRefId.Add(range.End.RefId);
                }
            }
        }

        var names = PropertyListHelper.GetPropertyNames(parameters);

        var sql = $"""
                   INSERT INTO {TskDbo.DboName} (
                       {names.Select(x => $"\"{x}\"").JoinStrings(",")}
                   )
                   SELECT *
                   FROM UNNEST({names.Select(x => $"@{x}").JoinStrings(",")})
                   ON CONFLICT DO NOTHING;
                   """;

        await _dbWriter.WriteAsync(sql, parameters);
    }
}
