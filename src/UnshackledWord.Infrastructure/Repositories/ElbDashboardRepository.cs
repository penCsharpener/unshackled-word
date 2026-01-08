using System.Text;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Repositories;
using UnshackledWord.Domain.Extensions;
using UnshackledWord.Domain.Models.Dbo;

namespace UnshackledWord.Infrastructure.Repositories;

public class ElbDashboardRepository : IElbDashboardRepository
{
    private readonly IDbReader _dbReader;

    public ElbDashboardRepository(IDbReader dbReader)
    {
        _dbReader = dbReader;
    }

    public async Task<Dictionary<int, string>> CreateBackupAsync(CancellationToken ct)
    {
        var sql = $"""
                  select ew."BibleBookId", ew."Chapter", ew."Verse", ew."{nameof(Elb1871WordDbo.WordInContext)}", ew."Lemma", ew."Strongs", ew."PartOfSpeech"
                  from {Elb1871WordDbo.DboName} ew
                  where (ew."{nameof(Elb1871WordDbo.Lemma)}" is not null or ew."{nameof(Elb1871WordDbo.Strongs)}" is not null or ew."{nameof(Elb1871WordDbo.PartOfSpeech)}" is not null)
                  order by ew."{nameof(Elb1871WordDbo.BibleBookId)}"
                         , ew."{nameof(Elb1871WordDbo.Chapter)}"
                         , ew."{nameof(Elb1871WordDbo.Verse)}"
                         , ew."{nameof(Elb1871WordDbo.PositionInVerse)}"
                         ;
                  """;

        var records = await _dbReader.ReadAsListAsync<ElbBackupRecord>(sql, ct);
        var dictionary = new Dictionary<int, string>();

        foreach (var group in records.GroupBy(x => x.BibleBookId))
        {
            var sb = new StringBuilder();

            foreach (var record in group.Select(x => x))
            {
                var setLemma = record.Lemma.IsNullOrWhiteSpace() ? null : $"\"{nameof(Elb1871WordDbo.Lemma)}\" = '{record.Lemma}'";
                var setStrongs = record.Strongs.IsNullOrWhiteSpace() ? null : $"\"{nameof(Elb1871WordDbo.Strongs)}\" = '{record.Strongs}'";
                var setPartOfSpeech = record.PartOfSpeech.IsNullOrWhiteSpace() ? null : $"\"{nameof(Elb1871WordDbo.PartOfSpeech)}\" = '{record.PartOfSpeech}'";

                var list = new List<string>();
                list.AddIfNotNull(setLemma)
                    .AddIfNotNull(setStrongs)
                    .AddIfNotNull(setPartOfSpeech);

                sb.AppendLine($"UPDATE {Elb1871WordDbo.DboName} " +
                              $"SET {list.JoinStrings(", ")} " +
                              $"WHERE \"{nameof(record.BibleBookId)}\" = {record.BibleBookId} " +
                              $"AND \"{nameof(record.Chapter)}\" = {record.Chapter} " +
                              $"AND \"{nameof(record.Verse)}\" = {record.Verse} " +
                              $"AND \"{nameof(record.WordInContext)}\" = '{record.WordInContext}';");
            }

            dictionary.Add(group.Key, sb.ToString());
        }

        return dictionary;
    }

    private record ElbBackupRecord(int BibleBookId, int Chapter, int Verse, string WordInContext, string? Lemma, string? Strongs, string? PartOfSpeech);
}
