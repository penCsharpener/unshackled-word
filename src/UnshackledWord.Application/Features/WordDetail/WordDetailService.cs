using System.Globalization;
using UnshackledWord.Application.Repositories;
using UnshackledWord.Domain.Models.Grammar;
using UnshackledWord.Domain.Models.Study;
using UnshackledWord.Domain.WebApi.Elberfelder.GetWordDetail;

namespace UnshackledWord.Application.Features.WordDetail;

public sealed class WordDetailService : IWordDetailService
{
    private const int LastHebrewBookId = 39;
    private const int ManyOriginalsThreshold = 3;

    private readonly IWordDetailRepository _words;
    private readonly IHebrewMorphemeRepository _morphemes;
    private readonly IStrongsLexiconRepository _lexicon;

    public WordDetailService(IWordDetailRepository words, IHebrewMorphemeRepository morphemes, IStrongsLexiconRepository lexicon)
    {
        _words = words;
        _morphemes = morphemes;
        _lexicon = lexicon;
    }

    public async Task<GetWordDetailResponse?> GetAsync(int wordId, CancellationToken token = default)
    {
        var word = await _words.GetWordAsync(wordId, token);
        if (word is null)
        {
            return null;
        }

        var isHebrew = word.BibleBookId <= LastHebrewBookId;
        IReadOnlyList<ReadingRowBase> rows = isHebrew
            ? await _words.GetHebrewReadingsAsync(wordId, token)
            : await _words.GetGreekReadingsAsync(wordId, token);

        IReadOnlyList<CoMappedWordRow> coRows = rows.Count == 0 ? [] : await _words.GetCoMappedWordsAsync(wordId, isHebrew, token);

        var readingRows = rows
            .Where(r => r.StepWordId is not null && !string.IsNullOrEmpty(r.Original))
            .OrderBy(r => r.PositionInVerse ?? int.MaxValue)
            .ThenBy(r => r.StepWordId)
            .ToList();

        var morphemesByWord = new Dictionary<int, List<HebrewMorpheme>>();
        if (isHebrew && readingRows.Count > 0)
        {
            var ids = readingRows.Select(r => r.StepWordId!.Value).Distinct().ToArray();
            foreach (var group in (await _morphemes.GetByStepWordIdsAsync(ids, token)).GroupBy(m => m.StepHebrewWordsId))
            {
                morphemesByWord[group.Key] = group.OrderBy(m => m.PositionInWord).ToList();
            }
        }

        var lexiconCache = new Dictionary<string, LexiconEntry?>();
        var readings = new List<OriginalReadingResponse>();
        foreach (var row in readingRows)
        {
            var strongs = CleanStrongs(row.Strongs);
            if (!lexiconCache.TryGetValue(strongs, out var entry))
            {
                entry = await FindLexiconEntryAsync(strongs, token);
                lexiconCache[strongs] = entry;
            }

            readings.Add(BuildReading(word, row, strongs, entry, morphemesByWord, coRows));
        }

        var response = new GetWordDetailResponse
        {
            WordId = word.Id,
            WordInContext = word.WordInContext,
            PositionInVerse = word.PositionInVerse,
            GermanLemma = NullIfEmpty(word.GermanLemma),
            IsAddedWord = rows.Count > 0 && rows.All(r => r.IsAddedWord == true && r.StepWordId is null) ? true : null,
            Readings = readings,
        };

        if (rows.Count > 0)
        {
            // The schema has no user/model/confirmation columns yet, so every mapping is reported as an AI mapping.
            response.Mapping = new MappingProvenanceResponse
            {
                Source = "ai",
                CreatedAt = FormatTimestamp(rows.Max(r => r.MappingCreatedAt)),
            };
        }

        var coMapped = BuildCoMappedWords(coRows);
        if (coMapped.Count > 0)
        {
            response.CoMappedWords = coMapped;
        }

        var hints = new List<string>();
        if (readings.Count >= ManyOriginalsThreshold)
        {
            hints.Add("manyOriginals");
        }

        if (rows.Any(r => r.GermanWordPart?.Trim() == "*"))
        {
            hints.Add("unclearWordPart");
        }

        if (hints.Count > 0)
        {
            response.Hints = hints;
        }

        return response;
    }

    private static OriginalReadingResponse BuildReading(
        ElbWordRow word,
        ReadingRowBase row,
        string strongs,
        LexiconEntry? entry,
        Dictionary<int, List<HebrewMorpheme>> morphemesByWord,
        IReadOnlyList<CoMappedWordRow> coRows)
    {
        var stepWordId = row.StepWordId!.Value;
        var reading = new OriginalReadingResponse
        {
            StepWordId = stepWordId,
            PositionInVerse = row.PositionInVerse ?? 0,
            GermanWordPart = CleanWordPart(row.GermanWordPart, word.WordInContext),
            Original = row.Original!,
            Transliteration = NullIfEmpty(row.Transliteration),
            Strongs = strongs,
            ContextGloss = NullIfEmpty(row.ContextGloss),
            Lexicon = BuildLexicon(strongs, entry),
        };

        var shared = coRows
            .Where(c => c.Relation == CoMappedRelations.SameOriginal && c.StepWordId == stepWordId)
            .OrderBy(c => c.PositionInVerse)
            .Select(c => c.WordId)
            .Distinct()
            .ToList();
        if (shared.Count > 0)
        {
            reading.SharedWithWordIds = shared;
        }

        switch (row)
        {
            case GreekReadingRow greek:
                reading.Language = "greek";
                reading.Lemma = NullIfEmpty(greek.Lemma) ?? entry?.OriginalWord;
                reading.Gloss = NullIfEmpty(greek.Gloss) ?? NullIfEmpty(entry?.Gloss);
                reading.Morphology = BuildGreekMorphology(greek);
                break;
            case HebrewReadingRow hebrew:
                var decoded = HebrewMorphologyDecoder.Decode(hebrew.MorphologyCode);
                reading.Language = decoded.IsAramaic ? "aramaic" : "hebrew";
                reading.Lemma = NullIfEmpty(entry?.OriginalWord);
                reading.Gloss = NullIfEmpty(entry?.Gloss);
                if (!string.IsNullOrWhiteSpace(hebrew.MorphologyCode))
                {
                    reading.Morphology = ToResponse(hebrew.MorphologyCode.Trim(), decoded.Root, null);
                }

                if (morphemesByWord.TryGetValue(stepWordId, out var morphemes) && morphemes.Count > 0)
                {
                    reading.Morphemes = BuildMorphemes(morphemes, decoded);
                }

                break;
        }

        return reading;
    }

    private static MorphologyResponse? BuildGreekMorphology(GreekReadingRow row)
    {
        if (string.IsNullOrWhiteSpace(row.MorphologyCode))
        {
            return null;
        }

        var keys = GreekMorphologyDecoder.Decode(row.MorphologyCode);
        string? description = null;
        if (keys.PartOfSpeech is null)
        {
            var parts = new[]
            {
                row.StepPartOfSpeech, row.StepTense, row.StepVoice, row.StepMood,
                row.StepPerson, row.StepCase, row.StepNumber, row.StepGender
            }.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
            description = parts.Count > 0 ? string.Join(", ", parts) : null;
        }

        return ToResponse(row.MorphologyCode.Trim(), keys, description);
    }

    private static List<MorphemeResponse> BuildMorphemes(List<HebrewMorpheme> morphemes, HebrewMorphologyResult decoded)
    {
        var zip = decoded.Segments.Count == morphemes.Count;
        var result = new List<MorphemeResponse>();
        var seenRoot = false;
        for (var i = 0; i < morphemes.Count; i++)
        {
            var m = morphemes[i];
            string role;
            if (m.IsRoot)
            {
                role = "root";
                seenRoot = true;
            }
            else
            {
                role = seenRoot ? "suffix" : "prefix";
            }

            MorphologyKeys keys;
            string? code;
            if (zip)
            {
                keys = decoded.Segments[i];
                code = decoded.SegmentCodes[i];
            }
            else
            {
                code = NullIfEmpty(m.Grammar);
                keys = HebrewMorphologyDecoder.Decode(code).Root;
                if (keys.IsEmpty)
                {
                    keys = HebrewMorphologyDecoder.DecodeSegment(code);
                }
            }

            result.Add(new MorphemeResponse
            {
                Role = role,
                Original = m.Hebrew ?? string.Empty,
                Strongs = NullIfEmpty(m.StrongsNumber),
                Morphology = code is null ? null : ToResponse(code, keys, null),
            });
        }

        return result;
    }

    private static MorphologyResponse ToResponse(string code, MorphologyKeys keys, string? description)
    {
        return new MorphologyResponse
        {
            Code = code,
            PartOfSpeech = keys.PartOfSpeech,
            Tense = keys.Tense,
            Voice = keys.Voice,
            Mood = keys.Mood,
            Person = keys.Person,
            Case = keys.Case,
            Number = keys.Number,
            Gender = keys.Gender,
            Degree = keys.Degree,
            Stem = keys.Stem,
            HebrewForm = keys.HebrewForm,
            State = keys.State,
            Description = description,
        };
    }

    private static LexiconSummaryResponse? BuildLexicon(string strongs, LexiconEntry? entry)
    {
        if (entry is null)
        {
            return null;
        }

        var (text, truncated) = LexiconTextCleaner.Clean(entry.Lexicon);
        return new LexiconSummaryResponse
        {
            Strongs = strongs,
            Lemma = entry.OriginalWord,
            Transliteration = NullIfEmpty(entry.Transliteration),
            Gloss = NullIfEmpty(entry.Gloss),
            Definition = NullIfEmpty(text),
            DefinitionTruncated = truncated ? true : null,
        };
    }

    private static List<CoMappedWordResponse> BuildCoMappedWords(IReadOnlyList<CoMappedWordRow> rows)
    {
        // parent > addedWord > sameOriginal when a word is related in several ways
        return rows
            .GroupBy(r => r.WordId)
            .Select(g => g.OrderBy(r => RelationPriority(r.Relation)).First())
            .OrderBy(r => r.PositionInVerse)
            .ThenBy(r => r.WordId)
            .Select(r => new CoMappedWordResponse
            {
                WordId = r.WordId,
                WordInContext = r.WordInContext,
                PositionInVerse = r.PositionInVerse,
                Relation = r.Relation,
            })
            .ToList();
    }

    private static int RelationPriority(string relation) => relation switch
    {
        CoMappedRelations.Parent => 0,
        CoMappedRelations.AddedWord => 1,
        _ => 2
    };

    private async Task<LexiconEntry?> FindLexiconEntryAsync(string strongs, CancellationToken token)
    {
        var parsed = StrongsNumber.Parse(strongs);
        if (parsed.Value is null)
        {
            return null;
        }

        var entries = await _lexicon.GetEntriesAsync(parsed.Value.Value, token);
        if (entries.Count == 0)
        {
            return null;
        }

        var extra = parsed.Value.Value.Extra;
        if (extra is not null)
        {
            var exact = entries.FirstOrDefault(e => string.Equals(e.Extra, extra, StringComparison.OrdinalIgnoreCase));
            if (exact is not null)
            {
                return exact;
            }
        }

        return entries.FirstOrDefault(e => string.IsNullOrEmpty(e.Extra)) ?? entries[0];
    }

    /// <summary>"H9003/{H7225G}" and "{H7225G}" become "H7225G".</summary>
    private static string CleanStrongs(string? strongs)
    {
        if (string.IsNullOrWhiteSpace(strongs))
        {
            return string.Empty;
        }

        var text = strongs.Trim();
        var open = text.IndexOf('{');
        var close = text.IndexOf('}');
        return open >= 0 && close > open ? text[(open + 1)..close].Trim() : text;
    }

    /// <summary>Omits the part when it is empty, "*" (unclear) or just the whole German word.</summary>
    private static string? CleanWordPart(string? part, string wordInContext)
    {
        var trimmed = part?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed == "*" || string.Equals(trimmed, wordInContext.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return trimmed;
    }

    private static string FormatTimestamp(DateTime value)
    {
        var utc = value.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(value, DateTimeKind.Utc) : value.ToUniversalTime();
        return utc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
