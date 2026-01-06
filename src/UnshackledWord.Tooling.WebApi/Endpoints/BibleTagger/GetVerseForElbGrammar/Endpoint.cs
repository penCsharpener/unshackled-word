using FastEndpoints;
using UnshackledWord.Application.Repositories;
using UnshackledWord.Domain.Models.Dbo;
using UnshackledWord.Domain.WebApi.BibleTagger.GetVerse;
using UnshackledWord.Domain.WebApi.BibleTagger.SaveElbGrammar;

namespace UnshackledWord.Tooling.WebApi.Endpoints.BibleTagger.GetVerseForElbGrammar;

public sealed class Endpoint : Ep.Req<GetVerseForElbGrammarRequest>.Res<GetVerseForElbGrammarResponse>
{
    private readonly IElb1871WordRepository _elbRepo;

    public Endpoint(IElb1871WordRepository elbRepo)
    {
        _elbRepo = elbRepo;
    }

    public override void Configure()
    {
        Get("/grammar/bookId/{BibleBookId:int}/chapterId/{ChapterId:int}/verseId/{VerseId:int}");
        Group<RouteGroupConfig>();
    }

    public override async Task<GetVerseForElbGrammarResponse> ExecuteAsync(GetVerseForElbGrammarRequest req, CancellationToken ct)
    {
        var elbWords = await _elbRepo.GetWordForVerseAsync(req.BibleBookId, req.ChapterId, req.VerseId, ct);

        var result = elbWords.Select(x => new Elb1871WordGrammarDto
        {
            Id = x.Id,
            Lemma = x.Lemma,
            PlainWord = x.PlainWord,
            BibleBookId = x.BibleBookId,
            Chapter = x.Chapter,
            Verse = x.Verse,
            GrammaticalKey = x.GrammaticalKey,
            PartOfSpeech = x.PartOfSpeech,
            PositionInVerse = x.PositionInVerse,
            Strongs = x.Strongs,
            WordInContext = x.WordInContext,
        }).ToList();

        var response = new GetVerseForElbGrammarResponse { ElberfelderWords = result };

        return response;
    }
}
