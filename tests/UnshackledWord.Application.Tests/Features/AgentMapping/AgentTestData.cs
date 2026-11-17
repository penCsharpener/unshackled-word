using UnshackledWord.Application.Features.AgentMapping;
using UnshackledWord.Application.Features.Lemmas;
using UnshackledWord.Domain.Models.Grammar;

namespace UnshackledWord.Application.Tests.Features.AgentMapping;

internal static class AgentTestData
{
    public static AgentGermanWordRow German(int id, string plain, string? lemma = null, string? stts = null, string? pos = null, int position = 0, int verse = 1, bool humanEdited = false, string? text = null) => new()
    {
        Id = id,
        Chapter = 1,
        Verse = verse,
        HebRefId = 41001000 + verse,
        PositionInVerse = position == 0 ? id % 1000 : position,
        Text = text ?? plain,
        Plain = plain,
        Lemma = lemma,
        Stts = stts,
        PartOfSpeech = pos,
        LemmaHumanEdited = humanEdited,
    };

    public static AgentSourceWordRow Source(int id, string strongs, string morphology = "N-NSM", int position = 0, int verse = 1) => new()
    {
        Id = id,
        RefId = 41001000 + verse,
        Position = position == 0 ? id % 1000 : position,
        Text = "w" + id % 1000,
        Lemma = "l" + id % 1000,
        Strongs = strongs,
        Gloss = "gloss" + id % 1000,
        Morphology = morphology,
    };

    public static AgentMappingRow Mapping(int german, int? source, int? author = null, bool ai = false, int? parent = null) => new()
    {
        ElbWordId = german,
        StepWordId = source,
        IsAddedWord = parent is not null,
        ParentElbWordId = parent,
        CreatedByUserId = author,
        AuthorIsAi = ai,
    };

    public static AgentMappingOptions Options(int k = 3, int nameK = 1) => new() { MinConfirmations = k, NameMinConfirmations = nameK };

    public static LemmaLexicon Lexicon(params AgentFormLemmaRow[] rows) => LemmaLexicon.Build(rows);

    public static AgentFormLemmaRow FormLemma(string form, string lemma, int count = 1, bool trusted = false) => new() { Form = form, Lemma = lemma, Count = count, Trusted = trusted };

    public static VerseDraft Pass(
        IReadOnlyList<AgentGermanWordRow> german,
        IReadOnlyList<AgentSourceWordRow> source,
        IReadOnlyList<AgentMappingRow>? mappings = null,
        LemmaLexicon? lexicon = null,
        ConfirmedStats? stats = null,
        AgentMode mode = AgentMode.Redo,
        bool hebrew = false,
        AgentMappingOptions? options = null) =>
        AgentDeterministicPass.Run(1, 1, "Mk 1:1", "hash", german, source, mappings ?? [], lexicon ?? LemmaLexicon.Build([]), stats ?? ConfirmedStats.Empty, options ?? Options(), mode, hebrew);

    /// <summary>
    /// Two verses. v1: g1 Anfang (open), g2 des (hint → s2), g3 Evangeliums (open), g4 Jesus (fixed → s4, fixed lemma), g5 ging VVFIN (open), g6 aus PTKVZ (open).
    /// v2: g7 Wort (open). Database ids: German 101..107, source 201..206.
    /// </summary>
    public static AgentPackageMap Map()
    {
        var v1 = new AgentVerseMap
        {
            Ref = "Mk 1:1", Chapter = 1, Verse = 1, Hash = "h1",
            German =
            {
                ["g1"] = new() { Id = 101, Text = "Anfang", Lemma = "Anfang", Stts = "NN", Pos = "NOUN" },
                ["g2"] = new() { Id = 102, Text = "des", Lemma = "der", Stts = "ART", Pos = "DET", Status = AgentTokenStatus.Hint },
                ["g3"] = new() { Id = 103, Text = "Evangeliums", Lemma = "Evangelium", Stts = "NN", Pos = "NOUN" },
                ["g4"] = new() { Id = 104, Text = "Jesu", Lemma = "Jesus", Stts = "NE", Pos = "PROPN", Status = AgentTokenStatus.Fixed, LemmaState = AgentLemmaState.Fixed },
                ["g5"] = new() { Id = 105, Text = "ging", Lemma = "gehen", Stts = "VVFIN", Pos = "VERB" },
                ["g6"] = new() { Id = 106, Text = "aus", Lemma = "aus", Stts = "PTKVZ", Pos = "ADP" },
            },
            Source =
            {
                ["s1"] = new() { Id = 201, Strongs = "G0746" },
                ["s2"] = new() { Id = 202, Strongs = "G3588", Exempt = true },
                ["s3"] = new() { Id = 203, Strongs = "G2098" },
                ["s4"] = new() { Id = 204, Strongs = "G2424G" },
                ["s5"] = new() { Id = 205, Strongs = "G1607" },
            },
            Fixed = { new() { G = "g4", S = "s4" } },
            Hints = { new() { G = "g2", S = "s2" } },
        };
        var v2 = new AgentVerseMap
        {
            Ref = "Mk 1:2", Chapter = 1, Verse = 2, Hash = "h2",
            German = { ["g7"] = new() { Id = 107, Text = "Wort", Lemma = "Wort", Stts = "NN", Pos = "NOUN" } },
            Source = { ["s6"] = new() { Id = 206, Strongs = "G3056" } },
        };

        return new AgentPackageMap { Language = "greek", Mode = AgentModes.Redo, BookId = 41, Verses = { ["v1"] = v1, ["v2"] = v2 } };
    }

    /// <summary>A complete, valid answer for <see cref="Map"/> (v1 and v2) as JSON text; tests replace parts of it.</summary>
    public const string GoodV1 = """
        {"v":"v1","links":[["g1","s1"],["g3","s3"]],"added":[],"lemmas":[],"parts":[],"unmapped":[["g5","verb of the next clause"],["g6","particle"]]}
        """;

    public const string GoodV2 = """{"v":"v2","links":[["g7","s6"]]}""";

    public static AgentAnswer Answer(params string[] verses) => AgentAnswerParser.Parse(System.Text.Json.JsonDocument.Parse($$"""{"verses":[{{string.Join(",", verses)}}]}""").RootElement);

    public static IReadOnlyList<AgentVerseValidation> Validate(AgentAnswer answer, AgentValidationContext? context = null, AgentPackageMap? map = null) =>
        new AgentProposalValidator().Validate(map ?? Map(), answer, context ?? Context());

    public static AgentValidationContext Context(IEnumerable<string>? known = null, Func<string, IReadOnlySet<string>>? human = null, ConfirmedStats? stats = null, bool stts = true) => new()
    {
        Stts = stts ? new SttsCatalog(
        [
            new SttsTag { Tag = "NN", Upos = "NOUN", AllowedUpos = ["NOUN"] },
            new SttsTag { Tag = "NE", Upos = "PROPN", AllowedUpos = ["PROPN", "NOUN"] },
            new SttsTag { Tag = "VVFIN", Upos = "VERB", AllowedUpos = ["VERB"] },
            new SttsTag { Tag = "VVINF", Upos = "VERB", AllowedUpos = ["VERB"] },
            new SttsTag { Tag = "PTKVZ", Upos = "ADP", AllowedUpos = ["ADP", "ADV", "PART"] },
            new SttsTag { Tag = "APPR", Upos = "ADP", AllowedUpos = ["ADP"] },
        ]) : null,
        KnownLemmas = (known ?? ["Anfang", "Evangelium", "Wort", "gehen", "ausgehen", "Jesus", "Haus", "laufen", "gehn"]).ToHashSet(),
        HumanLemmas = human ?? (_ => new HashSet<string>()),
        Stats = stats ?? ConfirmedStats.Empty,
        Options = Options(),
    };

    public static AgentVerseValidation Verse(IReadOnlyList<AgentVerseValidation> results, string v) => results.Single(r => r.V == v);
}
