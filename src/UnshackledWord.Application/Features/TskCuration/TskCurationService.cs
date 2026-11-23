using UnshackledWord.Domain.WebApi.Tsk;

namespace UnshackledWord.Application.Features.TskCuration;

/// <summary>Maintainer edits of TSK links (roadmap 0024.3): rating and comment of one existing TSK link, written with an audit row.</summary>
public sealed class TskCurationService
{
    private readonly ITskCurationRepository _repository;

    public TskCurationService(ITskCurationRepository repository)
    {
        _repository = repository;
    }

    /// <param name="canWrite">Admin or Maintainer. Until suggestions exist (0024.6) everyone else is refused.</param>
    public async Task<TskCurationOutcome> SaveAsync(int userId, bool canWrite, SaveTskCurationRequest request, CancellationToken token = default)
    {
        if (!canWrite)
        {
            return TskCurationOutcome.Forbidden("Only maintainers can curate cross references.");
        }

        var key = new TskLinkKey(request.SourceHebRefId, request.TargetStartHebRefId, request.TargetEndHebRefId);
        if (!await _repository.TskLinkExistsAsync(key, token))
        {
            return TskCurationOutcome.NotFound("No TSK cross reference with this source and target.");
        }

        var rating = string.IsNullOrWhiteSpace(request.Rating) ? null : request.Rating.Trim();
        var comment = NormaliseComment(request.Comment);
        var record = await _repository.ApplyAsync(key, state => state with { Rating = rating, Comment = comment }, userId, TskCurationSources.Edit, token);

        return TskCurationOutcome.Ok(ToResponse(key, record));
    }

    /// <summary>Trimmed; empty or whitespace only is no comment.</summary>
    public static string? NormaliseComment(string? comment) => string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();

    public static TskCurationResponse ToResponse(TskLinkKey key, TskCurationRecord? record) => new()
    {
        SourceHebRefId = key.SourceHebRefId,
        TargetStartHebRefId = key.TargetStartHebRefId,
        TargetEndHebRefId = key.TargetEndHebRefId,
        Origin = record?.State.Origin ?? TskOrigins.Tsk,
        Rating = record?.State.Rating,
        Comment = record?.State.Comment,
        UpdatedAt = record?.UpdatedAt,
    };
}
