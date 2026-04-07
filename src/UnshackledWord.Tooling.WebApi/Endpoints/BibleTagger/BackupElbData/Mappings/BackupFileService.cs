using System.Globalization;
using CsvHelper;
using Microsoft.Extensions.Options;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Features.Backup;
using UnshackledWord.Domain.Models.BibleStructure;
using UnshackledWord.Tooling.WebApi.Models;

namespace UnshackledWord.Tooling.WebApi.Endpoints.BibleTagger.BackupElbData.Mappings;

public class BackupFileService
{
    private readonly AppSettings _options;
    private readonly IElbDashboardRepository _repository;
    private readonly IFileService _file;

    public BackupFileService(IElbDashboardRepository repository, IFileService file, IOptions<AppSettings> options)
    {
        _options = options.Value;
        _repository = repository;
        _file = file;
    }

    public async Task WriteBackupAsync(CancellationToken token = default)
    {
        var backupPath = _options.BackupLocationPath;
        var mappingRows = await _repository.CreateBackupAsync(token);

        foreach (var (bookId, mappings) in mappingRows)
        {
            var book = BibleBook.AllBooks[bookId];

            await using var writer = new StreamWriter(_file.Combine(backupPath, $"{bookId.ToString().PadLeft(2,'0')}-{book.Name}.csv"));
            await using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
            await csv.WriteRecordsAsync(mappings, token);
        }
    }
}
