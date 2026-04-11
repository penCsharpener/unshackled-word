using UnshackledWord.Domain.Extensions;
using UnshackledWord.Domain.Models.BibleStructure;
using UnshackledWord.Domain.Models.Dbo;
using UnshackledWord.Tooling.SeedDb.Services.Abstractions;

namespace UnshackledWord.Tooling.SeedDb.Services.AiMappingImport;

public sealed class AiMappingImportRunner : IRunner
{
    private readonly AiMappingImportRunnerRepository _repository;
    private readonly ILogger<AiMappingImportRunner> _logger;

    public AiMappingImportRunner(AiMappingImportRunnerRepository repository, ILogger<AiMappingImportRunner> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task Run(CancellationToken token = default)
    {
        var gkCount = await _repository.GeMappingCountAsync("Greek");
        var hebCount = await _repository.GeMappingCountAsync("Hebrew");

        if (gkCount > 0 || hebCount > 0)
        {
            _logger.LogWarning("Mappings are already present: Greek {gkCount} / Hebrew {hebCount}", gkCount, hebCount);
            return;
        }

        var backupRowsDict = await _repository.ReadAllBackupsAsync(token);
        var stepWords = await _repository.ReadAllStepIdsAsync(token);
        var elbWords = await _repository.ReadAllElbIdsAsync(token);

        var greekMappings = new List<Elb1871MappingBase>(backupRowsDict.Where(x => x.Key > 40000000).Sum(x => x.Value.Count));
        var hebMappings = new List<Elb1871MappingBase>(backupRowsDict.Where(x => x.Key < 40000000).Sum(x => x.Value.Count));

        foreach (var (hebRefId, mappings) in backupRowsDict)
        {
            var bibRef = BibleReference.FromRefId(hebRefId);
            var elbWordsInVerse = elbWords[hebRefId];
            var stepWordsInVerse = stepWords.TryGetValue(hebRefId, out var stepList) ? stepList : [];

            if (stepWordsInVerse.IsNullOrEmpty())
            {
                _logger.LogInformation("{hebRefId}: Could not find verse in Step Words Dictionary", hebRefId);
                continue;
            }

            foreach (var mapping in mappings)
            {
                if (mapping.ElbWord.IsNullOrEmpty() && mapping.StepWord.IsNullOrEmpty())
                {
                    _logger.LogInformation("{hebRefId}: Both ElbWord and StepWord are nullOrEmpty", hebRefId);
                    continue;
                }

                mapping.GermanWordPart = mapping.GermanWordPart.IsNullOrEmpty() ? null : mapping.GermanWordPart;

                // clean up data from over-assignment
                if (mapping.StepWord.IsNotNullOrEmpty())
                {
                    mapping.ParentWord = null;
                    mapping.ParentPositionInVerse = null;
                    mapping.IsAddedWord = false;
                }

                if (mapping.ParentWord.IsNullOrEmpty() && mapping.IsAddedWord)
                {
                    mapping.IsAddedWord = false;
                }

                var elbWord = elbWordsInVerse.FirstOrDefault(x => x.PositionInVerse == mapping.PositionInVerse && x.ElbWord == mapping.ElbWord);
                if (elbWord is null)
                {
                    _logger.LogInformation("{hebRefId}:{position} ElbWord not found in Verse {elbWord}", hebRefId, mapping.PositionInVerse, mapping.ElbWord);
                    continue;
                }

                var stepWord = stepWordsInVerse.FirstOrDefault(x =>
                    x.PositionInVerse == mapping.StepPositionInVerse && x.StepWord == mapping.StepWord);
                var parentWord = elbWordsInVerse.FirstOrDefault(x => x.ElbWord == mapping.ParentWord && x.PositionInVerse == mapping.ParentPositionInVerse);

                if ((parentWord is null && mapping.IsAddedWord) || (parentWord is not null && !mapping.IsAddedWord))
                {
                    _logger.LogInformation("{hebRefId}:{position} mapping >> parent {parentWord} not found in verse", hebRefId, mapping.PositionInVerse, mapping.ParentWord);
                    continue;
                }

                if (stepWord is null && parentWord is null)
                {
                    if (mapping.StepWord.IsNotNullOrEmpty())
                    {
                        _logger.LogInformation("{hebRefId}:{position} StepWord {stepWord} not found in verse", hebRefId, mapping.PositionInVerse, mapping.StepWord);
                    }

                    _logger.LogInformation("{hebRefId}:{position} both StepWord {stepWord} and parent {parentWord} are null", hebRefId, mapping.PositionInVerse, mapping.StepWord, mapping.ParentWord);
                    continue;
                }

                var insert = new Elb1871MappingBase
                {
                    HebRefId = elbWord.HebRefId,
                    ElbWordId = elbWord.ElbWordId,
                    GermanWordPart = mapping.GermanWordPart,
                    IsAddedWord = mapping.IsAddedWord,
                    StepWordId = stepWord?.StepWordId,
                    ParentGermanWordId = parentWord?.ElbWordId,
                    PositionInVerse = mapping.PositionInVerse
                };

                if (bibRef.BookId >= 40)
                {
                    greekMappings.Add(insert);
                }

                if (bibRef.BookId <= 39)
                {
                    hebMappings.Add(insert);
                }
            }
        }

        await _repository.InsertMappingsAsync(greekMappings, "Greek");
        await _repository.InsertMappingsAsync(hebMappings, "Hebrew");
    }
}
