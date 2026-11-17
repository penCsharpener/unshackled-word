using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Features.Seed.Anchoring;
using UnshackledWord.Application.Features.Seed.Mappings;
using UnshackledWord.Application.Features.VerseMapping;

namespace UnshackledWord.Infrastructure.Seed.Mappings;

/// <summary>Read-only access for the mapping export: every mapping edge of a book with the keys of its German, STEP and parent word.</summary>
public sealed class MappingExportRepository
{
    private readonly IDbReader _dbReader;

    public MappingExportRepository(IDbReader dbReader)
    {
        _dbReader = dbReader;
    }

    private const string SelectGreekBook = """
        select
             w."HebRefId"        as "Ref"
            ,w."PositionInVerse" as "Pos"
            ,w."PlainWord"       as "Form"
            ,m."GermanWordPart"  as "Part"
            ,s."LxxRefId"        as "SrcRef"
            ,s."PositionInVerse" as "SrcPos"
            ,s."Greek"           as "SrcText"
            ,m."IsAddedWord"     as "Added"
            ,p."HebRefId"        as "ParentRef"
            ,p."PositionInVerse" as "ParentPos"
            ,p."PlainWord"       as "ParentForm"
            ,m."Role"            as "Role"
            ,null::varchar       as "Strongs"
            ,m."PartLemma"       as "PartLemma"
            ,m."MultiHead"       as "MultiHead"
        from "unshackled-word"."Elb1871GreekMapping" m
        join "unshackled-word"."Elb1871Words" w on w."Id" = m."ElbWordId"
        left join "unshackled-word"."StepGreekWords" s on s."Id" = m."StepWordId"
        left join "unshackled-word"."Elb1871Words" p on p."Id" = m."ParentGermanWordId"
        where w."BibleBookId" = @bookId
        order by w."HebRefId", w."PositionInVerse", s."LxxRefId", s."PositionInVerse"
        """;

    private const string SelectHebrewBook = """
        select
             w."HebRefId"        as "Ref"
            ,w."PositionInVerse" as "Pos"
            ,w."PlainWord"       as "Form"
            ,m."GermanWordPart"  as "Part"
            ,s."LxxRefId"        as "SrcRef"
            ,s."PositionInVerse" as "SrcPos"
            ,s."Hebrew"          as "SrcText"
            ,m."IsAddedWord"     as "Added"
            ,p."HebRefId"        as "ParentRef"
            ,p."PositionInVerse" as "ParentPos"
            ,p."PlainWord"       as "ParentForm"
            ,m."Role"            as "Role"
            ,m."StrongsNumber"   as "Strongs"
            ,m."PartLemma"       as "PartLemma"
            ,m."MultiHead"       as "MultiHead"
        from "unshackled-word"."Elb1871HebrewMapping" m
        join "unshackled-word"."Elb1871Words" w on w."Id" = m."ElbWordId"
        left join "unshackled-word"."StepHebrewWords" s on s."Id" = m."StepWordId"
        left join "unshackled-word"."Elb1871Words" p on p."Id" = m."ParentGermanWordId"
        where w."BibleBookId" = @bookId
        order by w."HebRefId", w."PositionInVerse", s."LxxRefId", s."PositionInVerse"
        """;

    private const string CountRows = """
        select
             (select count(*) from "unshackled-word"."Elb1871GreekMapping") + (select count(*) from "unshackled-word"."Elb1871HebrewMapping")
        """;

    private const string CountRowsWithoutWord = """
        select
             count(*)
        from (
            select "ElbWordId" from "unshackled-word"."Elb1871GreekMapping"
            union all
            select "ElbWordId" from "unshackled-word"."Elb1871HebrewMapping"
        ) m
        where not exists (select 1 from "unshackled-word"."Elb1871Words" w where w."Id" = m."ElbWordId")
        """;

    public async Task<List<MappingSeedLine>> ReadBookAsync(int bookId)
    {
        var sql = bookId >= MappingBooks.FirstGreekBookId ? SelectGreekBook : SelectHebrewBook;
        var rows = await _dbReader.ReadAsListAsync<ExportRow>(sql, new { bookId });

        // Two STEP words can share a position (75 positions in Hebrew), so the hash breaks the tie and the file does not depend on database ids.
        return rows.Select(ToLine)
            .OrderBy(l => l.Ref)
            .ThenBy(l => l.Pos)
            .ThenBy(l => l.SrcRef ?? -1)
            .ThenBy(l => l.SrcPos ?? -1)
            .ThenBy(l => l.SrcHash, StringComparer.Ordinal)
            .ToList();
    }

    public async Task<int> CountMappingRowsAsync()
    {
        return await _dbReader.ExecuteScalarAsync<int>(CountRows);
    }

    public async Task<int> CountRowsWithoutWordAsync()
    {
        return await _dbReader.ExecuteScalarAsync<int>(CountRowsWithoutWord);
    }

    private static MappingSeedLine ToLine(ExportRow row)
    {
        var part = CleanPart(row.Part, row.Form);

        return new MappingSeedLine
        {
            Ref = row.Ref,
            Pos = row.Pos,
            Form = row.Form ?? string.Empty,
            Part = part,
            SrcRef = row.SrcRef,
            SrcPos = row.SrcPos,
            SrcHash = row.SrcRef.HasValue ? SourceKey.Hash(row.SrcText) : string.Empty,
            Added = row.Added ?? false,
            ParentPos = row.ParentPos,
            ParentForm = row.ParentPos.HasValue ? row.ParentForm ?? string.Empty : string.Empty,
            ParentRef = row.ParentRef.HasValue && row.ParentRef != row.Ref ? row.ParentRef : null,
            Role = string.IsNullOrEmpty(row.Role) ? "head" : row.Role,
            Strongs = row.Strongs ?? string.Empty,
            PartLemma = part.Length > 0 ? row.PartLemma ?? string.Empty : string.Empty, // a part lemma only belongs to a part that is exported
            MultiHead = row.MultiHead ?? false
        };
    }

    /// <summary>
    /// A part that contains Greek or Hebrew letters is original-language text that slipped into the German part column (295 rows on the
    /// maintainer's database). STEP text must not be redistributed, so such a part is exported empty.
    /// </summary>
    internal static string CleanPart(string? part)
    {
        return string.IsNullOrEmpty(part) || ContainsOriginalLanguageLetters(part) ? string.Empty : part;
    }

    /// <summary>
    /// <see cref="CleanPart(string?)"/> plus: only a real compound part of the German word is exported (not the whole word, <c>1</c>, <c>null</c>
    /// ... see migration 0027). The format is unchanged, so older seed files stay importable (the importer applies the same rule).
    /// </summary>
    public static string CleanPart(string? part, string? form)
    {
        var cleaned = CleanPart(part);

        return GermanWordParts.IsRealPart(form, cleaned) ? cleaned : string.Empty;
    }

    public static bool ContainsOriginalLanguageLetters(string text)
    {
        return text.Any(c => c is (>= '\u0370' and <= '\u03FF') or (>= '\u1F00' and <= '\u1FFF') or (>= '\u0590' and <= '\u05FF') or (>= '\uFB1D' and <= '\uFB4F'));
    }

    private sealed class ExportRow
    {
        public int Ref { get; init; }
        public int Pos { get; init; }
        public string? Form { get; init; }
        public string? Part { get; init; }
        public int? SrcRef { get; init; }
        public int? SrcPos { get; init; }
        public string? SrcText { get; init; }
        public bool? Added { get; init; }
        public int? ParentRef { get; init; }
        public int? ParentPos { get; init; }
        public string? ParentForm { get; init; }
        public string? Role { get; init; }
        public string? Strongs { get; init; }
        public string? PartLemma { get; init; }
        public bool? MultiHead { get; init; }
    }
}

/// <summary>The book split between the two mapping tables: books 1-39 are Hebrew, 40-66 Greek.</summary>
public static class MappingBooks
{
    public const int FirstGreekBookId = 40;

    public static bool IsGreek(int hebRefId) => hebRefId / 1_000_000 >= FirstGreekBookId;
}
