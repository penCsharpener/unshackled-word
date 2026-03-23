using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Extensions;
using UnshackledWord.Domain.Models.BibleStructure;
using UnshackledWord.Domain.Models.Dbo;
using UnshackledWord.Tooling.SeedDb.Services.Abstractions;

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
        var insertRows = new List<string>();

        foreach (var tskReference in tskReferences)
        {
            foreach (var crossRef in tskReference.CrossReferences)
            {
                if (crossRef is BibleReference bibleRef)
                {
                    insertRows.Add($"({tskReference.Reference.RefId}, '{tskReference.Words.Replace("'", "''")}', {bibleRef.RefId}, null)");
                }

                if (crossRef is BibleReferenceRange range)
                {
                    insertRows.Add($"({tskReference.Reference.RefId}, '{tskReference.Words.Replace("'", "''")}', {range.Start.RefId}, {range.End.RefId})");
                }
            }
        }

        const int BatchSize = 20000;
        var batchCount = insertRows.Count / BatchSize;

        for (var i = 0; i <= batchCount; i++)
        {
            var batchRows = insertRows.Skip(i * BatchSize).Take(BatchSize).ToList();

            var insertSql = $"""
                             INSERT INTO {TskDbo.DboName}
                             ("LxxRefId", "Scope", "RelatedStartLxxRefId", "RelatedEndLxxRefId")
                             VALUES
                             {batchRows.JoinStrings(delimiter)}
                             ;
                             """;

            await _dbWriter.WriteAsync(insertSql, token);
        }
    }
}
