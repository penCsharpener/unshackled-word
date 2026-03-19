using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Abstractions.Step;
using UnshackledWord.Infrastructure.Repositories.Step;
using UnshackledWord.Tooling.SeedDb.Services.StepBible;
using UnshackledWord.Tooling.SeedDb.TestRunner.Configuration;

namespace UnshackledWord.Tooling.SeedDb.TestRunner.Services.StepBible;

public class StepHebrewStrongsNormalizingStrategyTests
{
    private readonly StepHebrewStrongsNormalizingStrategy _sut;
    private readonly IStepHebrewWordsRepository _wordsRepo;
    private readonly IStepHebrewWordsNormalizedRepository _normalizedRepo;
    private readonly IDbWriter _dbMockWriter;
    private readonly IDbReader _dbReader;
    private readonly IDbWriter _dbWriter;
    private ILogger<StepHebrewStrongsNormalizingStrategy> _logger;

    public StepHebrewStrongsNormalizingStrategyTests()
    {
        var sp = TestConfiguration.GetServiceProvider();
        _dbWriter = sp.GetRequiredService<IDbWriter>();
        _dbMockWriter = Substitute.For<IDbWriter>();
        _dbReader = sp.GetRequiredService<IDbReader>();
        _logger = Substitute.For<ILogger<StepHebrewStrongsNormalizingStrategy>>();
        _wordsRepo = new StepHebrewWordsRepository(_dbWriter, _dbReader);
        _normalizedRepo = sp.GetRequiredService<IStepHebrewWordsNormalizedRepository>();
        _sut = new StepHebrewStrongsNormalizingStrategy(_wordsRepo, _normalizedRepo, _logger);
    }

    [Fact]
    public async Task Run_StepHebrewStrongsNormalizingStrategy()
    {
        await _sut.SaveToDatabase("", CancellationToken.None);
    }
}
