using System.Text;
using Microsoft.Extensions.Options;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Repositories;
using UnshackledWord.Domain.Models.BibleStructure;
using UnshackledWord.Tooling.WebApi.Models;

namespace UnshackledWord.Tooling.WebApi.Endpoints.BibleTagger.BackupElbData;

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

        var dictionary = await _repository.CreateBackupAsync(token);

        foreach (var (bookId, sql) in dictionary)
        {
            var bibleBook = (BibleBook)bookId;
            var bookPath = _file.Combine(backupPath, $"70{bookId.ToString().PadLeft(2, '0')}_Elb1871Data_{bibleBook.Name.Replace(" ", "")}.sql");
            _file.DeleteFile(bookPath);

            await _file.WriteAllTextAsync(bookPath, sql, Encoding.UTF8, token);
        }
    }
}
