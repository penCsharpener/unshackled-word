using FastEndpoints;
using UnshackledWord.Domain.Models;

namespace UnshackledWord.Tooling.WebApi.Elberfelder.GetVerseForMapping;

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
            response.Translation1Words = new TranslationWord[]
            {
                new TranslationWord(new TypedId<TranslationWord>(1), "Buch"),
                new TranslationWord(new TypedId<TranslationWord>(2), "des"),
                new TranslationWord(new TypedId<TranslationWord>(3), "Geschlechts"),
                new TranslationWord(new TypedId<TranslationWord>(4), "Jesu"),
                new TranslationWord(new TypedId<TranslationWord>(5), "Christi"),
                new TranslationWord(new TypedId<TranslationWord>(6), "des"),
                new TranslationWord(new TypedId<TranslationWord>(7), "Sohnes"),
                new TranslationWord(new TypedId<TranslationWord>(8), "Davids"),
                new TranslationWord(new TypedId<TranslationWord>(9), "des"),
                new TranslationWord(new TypedId<TranslationWord>(10), "Sohnes"),
                new TranslationWord(new TypedId<TranslationWord>(11), "Abrahams."),
            };
            response.Translation2Words = new TranslationWord[]
            {
                new TranslationWord(new TypedId<TranslationWord>(1), "Buch"),
                new TranslationWord(new TypedId<TranslationWord>(2), "des"),
                new TranslationWord(new TypedId<TranslationWord>(3), "Geschlechts"),
                new TranslationWord(new TypedId<TranslationWord>(4), "Jesu"),
                new TranslationWord(new TypedId<TranslationWord>(5), "Christi"),
                new TranslationWord(new TypedId<TranslationWord>(6), "des"),
                new TranslationWord(new TypedId<TranslationWord>(7), "Sohnes"),
                new TranslationWord(new TypedId<TranslationWord>(8), "Davids"),
                new TranslationWord(new TypedId<TranslationWord>(9), "des"),
                new TranslationWord(new TypedId<TranslationWord>(10), "Sohnes"),
                new TranslationWord(new TypedId<TranslationWord>(11), "Abrahams."),
            };
            response.SourceWords = new SourceWord[]
            {
                new SourceWord(new TypedId<SourceWord>(1), "Βίβλος", "G976"),
                new SourceWord(new TypedId<SourceWord>(2), "γενέσεως", "G1078"),
                new SourceWord(new TypedId<SourceWord>(3), "Ἰησοῦ", "G2424"),
                new SourceWord(new TypedId<SourceWord>(4), "Χριστοῦ", "G5547"),
                new SourceWord(new TypedId<SourceWord>(5), "υἱοῦ", "G5207"),
                new SourceWord(new TypedId<SourceWord>(6), "Δαυὶδ", "G1138"),
                new SourceWord(new TypedId<SourceWord>(7), "υἱοῦ", "G5207"),
                new SourceWord(new TypedId<SourceWord>(8), "Ἀβραάμ", "G11"),
            };

            var json = """
                   {
                     "Translation1": [
                       { "id": 1, "word": "Buch" },
                       { "id": 2, "word": "des" },
                       { "id": 3, "word": "Geschlechts" },
                       { "id": 4, "word": "Jesu" },
                       { "id": 5, "word": "Christi," },
                       { "id": 6, "word": "des" },
                       { "id": 7, "word": "Sohnes" },
                       { "id": 8, "word": "Davids," },
                       { "id": 9, "word": "des" },
                       { "id": 10, "word": "Sohnes" },
                       { "id": 11, "word": "Abrahams." }
                     ],
                     "Translation2": [
                       { "id": 1, "word": "Buch" },
                       { "id": 2, "word": "des" },
                       { "id": 3, "word": "Geschlechts" },
                       { "id": 4, "word": "Jesu" },
                       { "id": 5, "word": "Christi," },
                       { "id": 6, "word": "des" },
                       { "id": 7, "word": "Sohnes" },
                       { "id": 8, "word": "Davids," },
                       { "id": 9, "word": "des" },
                       { "id": 10, "word": "Sohnes" },
                       { "id": 11, "word": "Abrahams." }
                     ],
                     "SourceLanguage": [
                       { "id": 1, "word": "Βίβλος", "strongs": "G976" },
                       { "id": 2, "word": "γενέσεως", "strongs": "G1078" },
                       { "id": 3, "word": "Ἰησοῦ", "strongs": "G2424" },
                       { "id": 4, "word": "Χριστοῦ", "strongs": "G5547" },
                       { "id": 5, "word": "υἱοῦ", "strongs": "G5207" },
                       { "id": 6, "word": "Δαυὶδ", "strongs": "G1138" },
                       { "id": 7, "word": "υἱοῦ", "strongs": "G5207" },
                       { "id": 8, "word": "Ἀβραάμ", "strongs": "G11" }
                     ]
                   }
                   """;

            return response;
        }

        return new GetVerseResponse();
    }
}

public record GetVerseRequest(int BookId, int ChapterId, int VerseId);

public sealed class GetVerseResponse
{
    public TranslationWord[] Translation1Words { get; set; } = [];
    public TranslationWord[] Translation2Words { get; set; } = [];
    public SourceWord[] SourceWords { get; set; } = [];
}

public class TranslationWord
{
    public TranslationWord(TypedId<TranslationWord> id, string word)
    {
        Id = id;
        Word = word;
    }

    public TypedId<TranslationWord> Id { get; set; }
    public string Word { get; set; } = default!;
}

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
