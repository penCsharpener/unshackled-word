using UnshackledWord.Domain.Models.BibleStructure;
using UnshackledWord.Tooling.AiWorker.Mapping.Models;
using UnshackledWord.Tooling.AiWorker.Mapping.Models.Hebrew;
using GeminiClient = Google.GenAI.Client;

namespace UnshackledWord.Tooling.AiWorker.Mapping.Hebrew;

public class HebrewGeminiFlashClient : GeminiMappingBaseClient
{
    // private const string ModelName = "gemini-3-flash-preview";
    // a) if there are multiple German words possible candidates for a parent then prefer less frequent words as parent, ie. 'er gemacht hatte' the parent is 'gemacht', not 'hatte'
    // ElbWordId|StepWordId|IsAddedWord|ParentElbWordId|PartOrder|GermanWordPart
    private const string HebrewSystemInstruction =
        """
        You are a linguistic expert specializing in mapping the German Elberfelder 1871 Bible to the Biblical Hebrew MT. Your task is to generate a precise word-level alignment in a specific JSON format.

        ### MAPPING RULES:
        1. SEQUENTIAL MAPPING: Map German words to Hebrew words following their appearance in the text.
        2. MORPHOLOGICAL ALIGNMENT (Many-to-One):
           - Map German possessive pronouns (e.g., "ihr") and their associated noun (e.g., "Vater") to the single Hebrew ID containing the suffix (e.g., "אָבִ֖יהָ").
           - Map auxiliary verbs or phrasal verb components to the single Hebrew ID representing the action.
        3. ADDED WORDS (IsAddedWord/ParentElbWordId):
           - If a German word has no Hebrew equivalent (e.g., articles like "ein", "der" or supplementary particles), set IsAddedWord = 1.
           - You MUST identify a ParentElbWordId for every IsAddedWord = 1. The parent is the primary German noun or verb the added word modifies.
        4. UNTRANSLATED HEBREW PARTICLES:
           - Link Hebrew untranslated markers (like 'אֶת־' or 'לְ') to the nearest semantic German noun or verb ID.
        5. COMPOUND WORDS (PartOrder/GermanWordPart):
           - If one German Word ID corresponds to multiple Hebrew Word IDs (e.g., "Gersten-ernte"), create two entries for that German ID.
           - Use PartOrder (1, 2, etc.) and GermanWordPart to show the split (e.g., "Gersten" and "ernte").
        6. DATA VALIDATION:
           - if IsAddedWord is 1 and ParentElbWordId is set, then StepWordId MUST be null
           - if StepWordId is set, IsAddedWord is false and ParentElbWordId null
           - if GermanWordPart and PartOrder are set, they have the same ElbWordId, otherwise they are null
           - GermanWordPart does not contain an ID, but the part of the German Word the Hebrew Id belongs to
           - only use Ids that have been submitted in the request
           - IMPORTANT: no StepId of one verse is mapped to a German word of another verse

        ### OUTPUT FORMAT:
        Return a JSON array of objects. Each object must contain:
        - "RefId": Integer (BookId * 1000000 + Chapter * 1000 + Verse).
        - "Data": An array of pipe-delimited strings: "ElbWordId|StepWordId|IsAddedWord|ParentElbWordId|PartOrder|GermanWordPart"

        ### DATA CONVENTIONS:
        - Use '1' for true, '0' for false.
        - Use '-' for null/empty values in ParentElbWordId, PartOrder, and GermanWordPart.
        - Ensure every Elberfelder ID provided in the input is accounted for in the output.
        """;

    public HebrewGeminiFlashClient(GeminiClient client, ILogger<HebrewGeminiFlashClient> logger) : base(client, logger) { }

    public async Task<List<VerseDataList<ElbStepAiMapping>>> GetElbStepMappings(List<VerseDataList<ElbVerseData>> elbWords,
        List<VerseDataList<StepHebrewVerseData>> stepWords, CancellationToken token = default)
    {
        // replace original ids with smaller ints starting from 1
        // use two dictionaries to be able to map the ids back after the response comes back
        // this should also further reduce the costs as ids are much shorter

        var germanVerseJson = elbWords.ToWithoutOrder().ToDelimitedString();
        var hebrewVerseJson = stepWords.ToWithoutOrder().ToDelimitedString();

        var prompt = $"""
                      German Words: {germanVerseJson}
                      Hebrew Words: {hebrewVerseJson}
                      """;

        var response = await SubmitAsync(prompt, HebrewSystemInstruction, GeminiModelType.Flash3_1LitePreview, token);
        var result = response.ToTypedResponse().ToList();

        foreach (var x in result)
        {
            x.RefId = new BibleReference(x.BookId, x.Chapter, x.Verse).RefId;
        }

        return AddInternalWords(result, elbWords, stepWords);
    }

    private List<VerseDataList<ElbStepAiMapping>> AddInternalWords(List<VerseDataList<ElbStepAiMapping>> mappings,
        List<VerseDataList<ElbVerseData>> elbWords,
        List<VerseDataList<StepHebrewVerseData>> stepWords)
    {
        var dictElb = new Dictionary<int, string>();
        foreach (var word in elbWords.SelectMany(x => x.Data))
        {
            dictElb.TryAdd(word.Id, word.German);
        }

        var dictStep = new Dictionary<int, string>();
        foreach (var word in stepWords.SelectMany(x => x.Data))
        {
            dictStep.TryAdd(word.Id, word.Hebrew);
        }

        foreach (var mapping in mappings.SelectMany(x => x.Data))
        {
            mapping.InternalElbWord = dictElb.TryGetValue(mapping.ElbWordId, out var value) ? value : null;
            mapping.InternalParentWord = mapping.ParentElbWordId.HasValue && dictElb.TryGetValue(mapping.ParentElbWordId.Value, out var value2) ? value2 : null;
            mapping.InternalStepWord = dictStep.TryGetValue(mapping.StepWordId ?? 0, out var value3) ? value3 : null;
        }

        return mappings;
    }
}
