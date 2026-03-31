using FastEndpoints;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Models.Dbo;
using UnshackledWord.Domain.WebApi.BibleTagger.Reading;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Elberfelder.GetWordsForChapter;

public class Endpoint : Ep.Req<GetWordsOfChapterRequest>.Res<GetWordsOfChapterResponse>
{
    private readonly IDbReader _reader;

    public Endpoint(IDbReader reader)
    {
        _reader = reader;
    }

    public override void Configure()
    {
        Get("elberfelder/chapterWords/bookId/{BibleBookId:int}/chapterId/{chapterId:int}");
    }

    public override async Task<GetWordsOfChapterResponse> ExecuteAsync(GetWordsOfChapterRequest req, CancellationToken ct)
    {
        var verses = await _reader.ReadAsListAsync<Elb1871WordDbo>(SqlFactory(req.BibleBookId, req.ChapterId));

        return new GetWordsOfChapterResponse
        {
            BibleBookId = req.BibleBookId,
            Chapter = req.ChapterId,
            Words = verses.Select(x =>
            {
                return new WordResponse
                {
                    Id = x.Id,
                    Lemma = x.Lemma,
                    PlainWord = x.PlainWord,
                    PositionInVerse = x.PositionInVerse,
                    Strongs = x.Strongs,
                    Verse = x.Verse,
                    WordInContext = x.WordInContext,
                    Original = x.Original,
                    GrammaticalKey = x.GrammaticalKey
                };
            }).ToList()
        };
    }

    private string SqlFactory(int bookId, int chapter)
    {
        if (bookId >= 1 && bookId <= 39)
        {
            return $"""
                    SELECT ew."Id"
                         , ew."Verse"
                         , ew."PositionInVerse"
                         , ew."WordInContext"
                         , ew."PlainWord"
                         , ehm."StrongsNumber" "Strongs"
                         , shwn."Hebrew" "Lemma"
                         , (SELECT shw."Hebrew"
                            FROM "unshackled-word"."StepHebrewWords" shw
                                INNER JOIN "unshackled-word"."StepHebrewWordsNormalizedToHebrewWords" shwnthw ON shwnthw."StepHebrewWordsId" = shw."Id"
                                                                                                                AND shw."BibleBookId" = {bookId}
                                                                                                                AND shw."Chapter" = {chapter}
                            WHERE shwnthw."StepHebrewWordsNormalizedId" = ehm."StepHebrewNormalizedId"
                            LIMIT 1) "Original"
                         , '' "GrammaticalKey"
                    FROM "unshackled-word"."Elb1871Words" ew
                        LEFT JOIN "unshackled-word"."Elb1871HebrewMapping" ehm ON ew."Id" = ehm."ElbWordId"
                        LEFT JOIN "unshackled-word"."StepHebrewWordsNormalized" shwn ON ehm."StepHebrewNormalizedId" = shwn."Id"
                    WHERE 1=1
                        AND ew."BibleBookId" = {bookId}
                        AND ew."Chapter" = {chapter}
                    ORDER BY ew."BibleBookId", ew."Chapter", ew."Verse", ew."PositionInVerse"
                    """;
        }

        return $"""
                SELECT ew."Id"
                     , ew."Verse"
                     , ew."PositionInVerse"
                     , ew."WordInContext"
                     , ew."PlainWord"
                     , egm."StrongsNumber" "Strongs"
                     , sgw."Lemma"
                     , sgw."Greek" "Original"
                     , sgw."Morphology" "GrammaticalKey"
                FROM "unshackled-word"."Elb1871Words" ew
                    LEFT JOIN "unshackled-word"."Elb1871GreekMapping" egm ON ew."Id" = egm."ElbWordId"
                    LEFT JOIN "unshackled-word"."StepGreekWords" sgw ON egm."StepGreekId" = sgw."Id"
                WHERE 1=1
                    AND ew."BibleBookId" = {bookId}
                    AND ew."Chapter" = {chapter}
                ORDER BY ew."BibleBookId", ew."Chapter", ew."Verse", ew."PositionInVerse"
                """;
    }
}
