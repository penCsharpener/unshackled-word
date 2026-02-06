using System.Text;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Extensions;
using UnshackledWord.Domain.Models.Grammar;
using UnshackledWord.Tooling.SeedDb.Models;
using UnshackledWord.Tooling.SeedDb.Services.Abstractions;

namespace UnshackledWord.Tooling.SeedDb.Services.StatisticalRestorationGnt;

public sealed class SrTsvParserStrategy : IFileParserStrategy
{
    private readonly IFileService _fileService;
    private readonly IDbWriter _dbWriter;
    private readonly IDbReader _reader;
    private readonly ParseHelper _parseHelper;

    public SrTsvParserStrategy(IFileService fileService, IDbWriter dbWriter, IDbReader reader, ParseHelper parseHelper)
    {
        _fileService = fileService;
        _dbWriter = dbWriter;
        _reader = reader;
        _parseHelper = parseHelper;
    }

    public async Task SaveToDatabase(string filePath, CancellationToken token = default)
    {
        var select = """
                     SELECT *
                     FROM "unshackled-word"."SrGntWords"
                     LIMIT 1;
                     """;
        var existingRows = await _reader.ReadFirstOrDefaultAsync<object>(select);

        if (existingRows is not null)
        {
            return;
        }

        var lines = await _fileService.ReadAllLinesAsync(filePath, Encoding.UTF8, token);
        var wordList = new List<WordInfo>();
        var positionInVerse = 1;
        var verseId = 0;

        foreach (var line in lines.Skip(1))
        {
            var parts = line.Split('\t');
            if (parts.Length < 7)
            {
                continue;
            }

            var bibleBook = _parseHelper.ParseNtBookId(parts[0]);
            var reference = _parseHelper.ParseNtVerseId(parts[0]);
            var wordInContext = parts[1];
            var koineWord = parts[2];
            var lemma = parts[3];
            var strongs = parts[4];
            if (strongs.EndsWith('0'))
            {
                strongs = strongs[..^1];
            }

            var partOfSpeech = parts[5];
            var conjugation = parts[6];
            var grammaticalKey = ParseGrammaticalKey(conjugation);

            if (reference.Verse != verseId)
            {
                positionInVerse = 1;
            }
            else
            {
                positionInVerse++;
            }

            verseId = reference.Verse;

            var wordInfo = new WordInfo(
                bibleBook,
                bibleBook.Id,
                reference.Chapter,
                reference.Verse,
                positionInVerse,
                wordInContext,
                koineWord,
                lemma,
                $"G{strongs}",
                ParsePartOfSpeechAbbreviation(partOfSpeech).ToString(),
                conjugation,
                grammaticalKey
            );

            wordList.Add(wordInfo);
        }

        await InsertAsync(wordList, 50);
    }

    private async Task InsertAsync(WordInfo wordInfo)
    {
        var sql = """
                  INSERT INTO "unshackled-word"."SrGntWords"
                  ("BibleBookId"", "Chapter", "Verse", "WordInContext", "Koine", "Lemma", "PositionInVerse", "Strongs", "PartOfSpeech", "GrammaticalKey")
                  VALUES (@BibleBookId, @WordInContext, @Koine, @Lemma, @PositionInVerse, @Strongs, @PartOfSpeech, @ConjugationKey);
                  """;

        await _dbWriter.WriteAsync(sql, wordInfo);
    }

    private async Task InsertAsync(List<WordInfo> wordInfos, int bulkSize)
    {
        var sql = """
                  INSERT INTO "unshackled-word"."SrGntWords"
                  ("BibleBookId", "Chapter", "Verse", "WordInContext", "Koine", "Lemma", "PositionInVerse", "Strongs", "PartOfSpeech", "GrammaticalKey", "Mood", "Tense", "Voice", "Person", "Case", "Gender", "Number")
                  VALUES
                  """;

        var counter = 1;
        var batch = new List<string>();

        foreach (var wordInfo in wordInfos)
        {
            batch.Add($"({wordInfo.BibleBookId}, {wordInfo.Chapter}, {wordInfo.Verse}, '{wordInfo.WordInContext}', " +
                      $"'{wordInfo.Koine}', '{wordInfo.Lemma}', '{wordInfo.PositionInVerse}', '{wordInfo.Strongs}', " +
                      $"'{wordInfo.PartOfSpeech}', '{wordInfo.ConjugationKey}', {ConvertToSql((int?)wordInfo.GrammaticalKey.Mood)}, " +
                      $"{ConvertToSql((int?)wordInfo.GrammaticalKey.Tense)}, {ConvertToSql((int?)wordInfo.GrammaticalKey.Voice)}, " +
                      $"{ConvertToSql((int?)wordInfo.GrammaticalKey.Person)}, {ConvertToSql((int?)wordInfo.GrammaticalKey.Case)}, " +
                      $"{ConvertToSql((int?)wordInfo.GrammaticalKey.Gender)}, {ConvertToSql((int?)wordInfo.GrammaticalKey.Number)})");
            counter++;

            if (counter == bulkSize)
            {
                await WriteBatch(sql, batch);

                batch.Clear();
                counter = 1;
            }
        }

        await WriteBatch(sql, batch);
    }

    private string ConvertToSql(int? number)
    {
        return number.HasValue ? number.Value.ToString() : "NULL";
    }

    private async Task WriteBatch(string command, List<string> rows)
    {
        var insert = command + Environment.NewLine + rows.JoinStrings($",{Environment.NewLine}") + ";";
        await _dbWriter.WriteAsync(insert);
    }

    private PartOfSpeech ParsePartOfSpeechAbbreviation(string abbreviation)
    {
        return abbreviation switch
        {
            "N" => PartOfSpeech.Noun,
            "V" => PartOfSpeech.Verb,
            "E" => PartOfSpeech.Article,
            "R" => PartOfSpeech.Pronoun,
            "C" => PartOfSpeech.Conjunction,
            "P" => PartOfSpeech.Preposition,
            "D" => PartOfSpeech.Adverb,
            "S" or "A" => PartOfSpeech.Adjective,
            "T" => PartOfSpeech.Particle,
            "I" => PartOfSpeech.Interjection,
            _ => throw new ArgumentOutOfRangeException(nameof(abbreviation), abbreviation, null)
        };
    }

    private GrammaticalKey ParseGrammaticalKey(string grammaticalKey)
    {
        var keys = grammaticalKey.ToCharArray();
        var moodKey = keys[0];
        var tenseKey = keys[1];
        var voiceKey = keys[2];
        var personKey = int.TryParse(keys[3].ToString(), out int person) ? person : (int?)null;
        var caseKey  = keys[4];
        var genderKey = keys[5];
        var numberKey = keys[6];

        return new GrammaticalKey()
        {
            Case = ParseCase(caseKey),
            Gender = ParseGender(genderKey),
            Mood = ParseMood(moodKey),
            Number = ParseNumber(numberKey),
            Person = personKey,
            Tense = ParseTense(tenseKey),
            Voice = ParseVoice(voiceKey),
        };
    }

    private GreekVoice? ParseVoice(char voiceKey)
    {
        return voiceKey switch
        {
            'A' => GreekVoice.Active,
            'M' => GreekVoice.Middle,
            'P' => GreekVoice.Passive,
            'Z' => GreekVoice.EitherMiddleOrPassive,
            _ => null
        };
    }

    private GreekTense? ParseTense(char key)
    {
        return key switch
        {
            'A' => GreekTense.Aorist,
            'F' => GreekTense.Future,
            'U' => GreekTense.FuturePerfect,
            'I' => GreekTense.Imperfect,
            'E' => GreekTense.Perfect,
            'L' => GreekTense.Pluperfect,
            'P' => GreekTense.Present,
            _ => null
        };
    }

    private GrammaticalNumber? ParseNumber(char key)
    {
        return key switch
        {
            'S' => GrammaticalNumber.Singular,
            'P' => GrammaticalNumber.Plural,
            'D' => GrammaticalNumber.Dual,
            _ => null
        };
    }

    private Gender? ParseGender(char key)
    {
        return key switch
        {
            'M' => Gender.Masculine,
            'F' => Gender.Feminine,
            'N' => Gender.Neuter,
            _ => null
        };
    }

    private GreekCase? ParseCase(char key)
    {
        return key switch
        {
            'V' => GreekCase.Vocative,
            'N' => GreekCase.Nominative,
            'G' => GreekCase.Genitive,
            'D' => GreekCase.Dative,
            'A' => GreekCase.Accusative,
            _ => null
        };
    }

    private GreekMood? ParseMood(char abbreviation)
    {
        return abbreviation switch
        {
            'I' => GreekMood.Indicative,
            'M' => GreekMood.Imperative,
            'N' => GreekMood.Infinitve,
            'O' => GreekMood.Optative,
            'S' => GreekMood.Subjunctive,
            'P' => GreekMood.Participle,
            _ => null
        };
    }

    public class GrammaticalKey
    {
        public GreekMood? Mood { get; set; }
        public GreekTense? Tense { get; set; }
        public GreekVoice? Voice { get; set; }
        public int? Person { get; set; }
        public GreekCase? Case { get; set; }
        public Gender? Gender { get; set; }
        public GrammaticalNumber? Number { get; set; }
    }
}
