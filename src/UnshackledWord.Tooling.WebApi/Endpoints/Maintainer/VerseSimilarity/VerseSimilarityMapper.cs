using UnshackledWord.Application.Features.VerseSimilarity;
using UnshackledWord.Domain.WebApi.Maintainer;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.VerseSimilarity;

internal static class VerseSimilarityMapper
{
    public static VerseSimilarityParametersResponse ToResponse(VerseSimilarityParameters p) => new()
    {
        StopDfShare = p.StopDfShare,
        TopK = p.TopK,
        MinScore = p.MinScore,
        PhraseWeight = p.PhraseWeight
    };

    public static VerseSimilarityRunResponse ToResponse(VerseSimilarityRunListItem item)
    {
        var r = item.Run;

        return new VerseSimilarityRunResponse
        {
            Id = r.Id,
            StartedAt = r.StartedAt,
            FinishedAt = r.FinishedAt,
            Status = r.Status,
            StartedBy = r.StartedBy,
            StartedByName = item.StartedByName,
            Parameters = new VerseSimilarityParametersResponse
            {
                StopDfShare = (double)r.StopDfShare,
                TopK = r.TopK,
                MinScore = (double)r.MinScore,
                PhraseWeight = (double)r.PhraseWeight
            },
            PairCount = r.PairCount,
            StopWordCount = r.StopWordCount,
            DurationMs = r.DurationMs,
            Evaluation = r.Evaluation,
            Error = r.Error
        };
    }
}
