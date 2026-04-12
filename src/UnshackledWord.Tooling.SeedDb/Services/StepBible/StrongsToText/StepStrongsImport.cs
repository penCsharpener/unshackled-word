using UnshackledWord.Application.Abstractions.Step;
using UnshackledWord.Tooling.SeedDb.Services.Abstractions;

namespace UnshackledWord.Tooling.SeedDb.Services.StepBible.StrongsToText;

public class StepStrongsImport : IRunner
{
    private readonly StepStrongsRepository _stepStrongsRepository;
    private readonly IStepStrongsNumbersRepository _stepStrongsNumbersRepository;

    public StepStrongsImport(StepStrongsRepository stepStrongsRepository, IStepStrongsNumbersRepository stepStrongsNumbersRepository)
    {
        _stepStrongsRepository = stepStrongsRepository;
        _stepStrongsNumbersRepository = stepStrongsNumbersRepository;
    }

    public async Task Run(CancellationToken token = default)
    {
        var strongs = await _stepStrongsRepository.GetOriginalStrongs();

        var mappedStrongs = strongs.ToDbo().ToList();

        await _stepStrongsNumbersRepository.BulkInsertInternalNewAsync(mappedStrongs, token);
    }
}
