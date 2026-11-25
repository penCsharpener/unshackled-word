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
            EvaluationResult = ToResponse(VerseSimilarityEvaluation.FromJson(r.Evaluation)),
            Error = r.Error
        };
    }

    private static VerseSimilarityEvaluationResponse? ToResponse(VerseSimilarityEvaluation? e) => e is null
        ? null
        : new VerseSimilarityEvaluationResponse
        {
            TopK = e.TopK,
            Testaments = (e.Testaments ?? []).Select(t => new VerseSimilarityTestamentRecallResponse
            {
                Testament = t.Testament,
                Links = t.Links,
                HitsAt10 = t.HitsAt10,
                HitsAt30 = t.HitsAt30,
                RecallAt10 = t.RecallAt10,
                RecallAt30 = t.RecallAt30
            }).ToList(),
            GoldenPairs = (e.GoldenPairs ?? []).Select(g => new VerseSimilarityGoldenRankResponse
            {
                Source = g.Source,
                SourceVerseId = g.SourceVerseId,
                Target = g.Target,
                TargetVerseId = g.TargetVerseId,
                Rank = g.Rank,
                ReverseRank = g.ReverseRank
            }).ToList(),
            Error = e.Error
        };
}
