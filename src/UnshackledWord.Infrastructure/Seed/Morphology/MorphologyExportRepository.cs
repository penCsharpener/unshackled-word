using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Features.Seed.Morphology;

namespace UnshackledWord.Infrastructure.Seed.Morphology;

/// <summary>Read-only access for the morphology export: every German word of a book with its morphology row, if any.</summary>
public sealed class MorphologyExportRepository
{
    private readonly IDbReader _dbReader;

    public MorphologyExportRepository(IDbReader dbReader)
    {
        _dbReader = dbReader;
    }

    private const string SelectBook = """
        select
             w."HebRefId"        as "Ref"
            ,w."PositionInVerse" as "Pos"
            ,w."PlainWord"       as "Form"
            ,m."Lemma"           as "Lemma"
            ,m."PartOfSpeech"    as "Upos"
            ,m."Stts"            as "Stts"
            ,m."Degree"          as "Degree"
            ,m."VerbForm"        as "VerbForm"
            ,m."Tense"           as "Tense"
            ,m."Person"          as "Person"
            ,m."Number"          as "Number"
            ,m."Mood"            as "Mood"
            ,m."Case"            as "Case"
            ,m."Gender"          as "Gender"
        from "unshackled-word"."Elb1871Words" w
        left join "unshackled-word"."Elb1871Morphology" m on m."HebRefId" = w."HebRefId" and m."PositionInVerse" = w."PositionInVerse"
        where w."BibleBookId" = @bookId
        order by w."HebRefId", w."PositionInVerse"
        """;

    private const string CountMorphology = """
        select
             count(*)
        from "unshackled-word"."Elb1871Morphology"
        """;

    public async Task<List<MorphologySeedLine>> ReadBookAsync(int bookId)
    {
        var rows = await _dbReader.ReadAsListAsync<ExportRow>(SelectBook, new { bookId });

        return rows.Select(ToLine).ToList();
    }

    public async Task<int> CountMorphologyRowsAsync()
    {
        return await _dbReader.ExecuteScalarAsync<int>(CountMorphology);
    }

    private static MorphologySeedLine ToLine(ExportRow row)
    {
        var features = new MorphologyFeatureValues(row.Degree, row.VerbForm, row.Tense, row.Person, row.Number, row.Mood, row.Case, row.Gender);

        return new MorphologySeedLine
        {
            Ref = row.Ref,
            Pos = row.Pos,
            Form = row.Form ?? string.Empty,
            Lemma = row.Lemma ?? string.Empty,
            Upos = row.Upos ?? string.Empty,
            Stts = row.Stts ?? string.Empty,
            Feats = MorphologyFeatures.Format(features)
        };
    }

    private sealed class ExportRow
    {
        public int Ref { get; init; }
        public int Pos { get; init; }
        public string? Form { get; init; }
        public string? Lemma { get; init; }
        public string? Upos { get; init; }
        public string? Stts { get; init; }
        public string? Degree { get; init; }
        public string? VerbForm { get; init; }
        public string? Tense { get; init; }
        public string? Person { get; init; }
        public string? Number { get; init; }
        public string? Mood { get; init; }
        public string? Case { get; init; }
        public string? Gender { get; init; }
    }
}
