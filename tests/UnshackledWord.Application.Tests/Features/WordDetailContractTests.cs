using System.Text.Json;
using System.Text.Json.Serialization;
using UnshackledWord.Domain.WebApi.Elberfelder.GetWordDetail;

namespace UnshackledWord.Application.Tests.Features;

public class WordDetailContractTests
{
    // Same options as the response serializer in Tooling.WebApi/Program.cs
    private static readonly JsonSerializerOptions Options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    [Fact]
    public void Serialize_UsesCamelCaseAndOmitsNulls()
    {
        var response = new GetWordDetailResponse
        {
            WordId = 26001,
            WordInContext = "Wort",
            PositionInVerse = 5,
            GermanLemma = "Wort",
            Mapping = new MappingProvenanceResponse { Source = "ai", CreatedAt = "2026-03-14T10:00:00Z" },
            CoMappedWords = [new CoMappedWordResponse { WordId = 26000, WordInContext = "das", PositionInVerse = 4, Relation = "addedWord" }],
            Readings =
            [
                new OriginalReadingResponse
                {
                    StepWordId = 140005,
                    Language = "greek",
                    PositionInVerse = 5,
                    Original = "λόγος",
                    Strongs = "G3056",
                    Morphology = new MorphologyResponse { Code = "N-NSM", PartOfSpeech = "noun", Case = "nominative" },
                    Lexicon = new LexiconSummaryResponse { Strongs = "G3056", Lemma = "λόγος", DefinitionTruncated = true },
                }
            ],
        };

        var json = JsonSerializer.Serialize(response, Options);

        json.ShouldBe(
            "{\"wordId\":26001,\"wordInContext\":\"Wort\",\"positionInVerse\":5,\"germanLemma\":\"Wort\"," +
            "\"readings\":[{\"stepWordId\":140005,\"language\":\"greek\",\"positionInVerse\":5,\"original\":\"\\u03BB\\u03CC\\u03B3\\u03BF\\u03C2\"," +
            "\"strongs\":\"G3056\",\"morphology\":{\"code\":\"N-NSM\",\"partOfSpeech\":\"noun\",\"case\":\"nominative\"}," +
            "\"lexicon\":{\"strongs\":\"G3056\",\"lemma\":\"\\u03BB\\u03CC\\u03B3\\u03BF\\u03C2\",\"definitionTruncated\":true}}]," +
            "\"mapping\":{\"source\":\"ai\",\"createdAt\":\"2026-03-14T10:00:00Z\"}," +
            "\"coMappedWords\":[{\"wordId\":26000,\"wordInContext\":\"das\",\"positionInVerse\":4,\"relation\":\"addedWord\"}]}");
    }

    [Fact]
    public void Serialize_CrossReferences_OmitsEndVerseForSingleVerses()
    {
        var json = JsonSerializer.Serialize(new GetWordDetailResponse
        {
            WordId = 1, WordInContext = "x", PositionInVerse = 2,
            CrossReferences = [new CrossReferenceResponse { BibleBookId = 1, Chapter = 1, Verse = 1 }, new CrossReferenceResponse { BibleBookId = 20, Chapter = 8, Verse = 22, EndVerse = 24 }],
            CrossReferencesTruncated = true,
        }, Options);

        json.ShouldBe(
            "{\"wordId\":1,\"wordInContext\":\"x\",\"positionInVerse\":2,\"readings\":[]," +
            "\"crossReferences\":[{\"bibleBookId\":1,\"chapter\":1,\"verse\":1},{\"bibleBookId\":20,\"chapter\":8,\"verse\":22,\"endVerse\":24}]," +
            "\"crossReferencesTruncated\":true}");
    }

    [Fact]
    public void Serialize_UnmappedWord_HasEmptyReadingsArrayAndNothingElse()
    {
        var json = JsonSerializer.Serialize(new GetWordDetailResponse { WordId = 1, WordInContext = "x", PositionInVerse = 2 }, Options);

        json.ShouldBe("{\"wordId\":1,\"wordInContext\":\"x\",\"positionInVerse\":2,\"readings\":[]}");
    }
}
