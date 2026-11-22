using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Features.Vocab.Session;
using UnshackledWord.Domain.WebApi.Vocab;

namespace UnshackledWord.Infrastructure.Vocab;

/// <summary>The learner's vocab settings (roadmap 0022.9): which quiz types are turned off. No row = nothing turned off.</summary>
public sealed class VocabSettingsService
{
    private readonly IDbConnectionFactory _connectionFactory;

    public VocabSettingsService(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<VocabSettingsResponse> GetAsync(int userId, CancellationToken token)
    {
        using var connection = await _connectionFactory.CreateDbConnectionAsync(token);
        using var transaction = connection.BeginTransaction();

        return await GetAsync(connection, transaction, userId);
    }

    public async Task<VocabSettingsResponse> SetAsync(int userId, IEnumerable<string> disabled, CancellationToken token)
    {
        using var connection = await _connectionFactory.CreateDbConnectionAsync(token);
        using var transaction = connection.BeginTransaction();
        var response = await SetAsync(connection, transaction, userId, disabled);
        transaction.Commit();

        return response;
    }

    public static async Task<VocabSettingsResponse> GetAsync(System.Data.IDbConnection c, System.Data.IDbTransaction t, int userId) =>
        ToResponse(await VocabSessionRepository.GetDisabledQuizTypesAsync(c, t, userId));

    /// <summary>Stores the list (deduplicated, in the order of <see cref="VocabQuizTypes.Toggleable"/>); the endpoint validates it first.</summary>
    public static async Task<VocabSettingsResponse> SetAsync(System.Data.IDbConnection c, System.Data.IDbTransaction t, int userId, IEnumerable<string> disabled)
    {
        var distinct = VocabQuizTypes.Toggleable.Where(type => disabled.Contains(type)).ToArray();
        await VocabSessionRepository.SetDisabledQuizTypesAsync(c, t, userId, distinct);

        return ToResponse(distinct);
    }

    private static VocabSettingsResponse ToResponse(string[] disabled) => new()
    {
        DisabledQuizTypes = disabled.ToList(),
        AvailableQuizTypes = VocabQuizTypes.Toggleable.Where(VocabQuizTypes.Implemented.Contains).ToList(),
    };
}
