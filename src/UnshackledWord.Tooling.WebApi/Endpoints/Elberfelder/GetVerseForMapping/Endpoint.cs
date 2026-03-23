using FastEndpoints;
using UnshackledWord.Domain.Models;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Elberfelder.GetVerseForMapping;

public class Endpoint : Ep.Req<GetVerseRequest>.Res<GetVerseResponse>
{
    public Endpoint()
    {

    }

    public override void Configure()
    {
        Get("elberfelder/bookId/{bookId:int}/chapterId/{chapterId:int}/verseId/{verseId:int}");
    }

    public override async Task<GetVerseResponse> ExecuteAsync(GetVerseRequest req, CancellationToken ct)
    {
        if (req.BookId == 40 && req.ChapterId == 1 && req.VerseId == 1)
        {
            var response = new GetVerseResponse();

            return response;
        }

        return new GetVerseResponse();
    }
}

public record GetVerseRequest(int BookId, int ChapterId, int VerseId);

public sealed class GetVerseResponse;

public class SourceWord
{
    public SourceWord(TypedId<SourceWord> id, string word, string strongs)
    {
        Id = id;
        Word = word;
        Strongs = strongs;
    }

    public TypedId<SourceWord> Id { get; set; }
    public string Word { get; set; } = default!;
    public string Strongs { get; set; } = default!;
}
