using UnshackledWord.Domain.Models.Grammar;

namespace UnshackledWord.Domain.Tests.Model.Grammar;

public class SttsTagDriftTests
{
    private sealed record CsvTag(string Tag, string LabelDe, string LabelEn, string Upos, string[] AllowedUpos);

    private static string FindSeedCsv()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "assets", "seed", "stts-tags.csv");
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }

        throw new FileNotFoundException("assets/seed/stts-tags.csv not found above " + AppContext.BaseDirectory);
    }

    /// <summary>Minimal RFC 4180 reader (quoted fields, doubled quotes, commas and line breaks inside quotes).</summary>
    private static List<List<string>> ParseCsv(string text)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new System.Text.StringBuilder();
        var quoted = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                else if (c == '"') quoted = false;
                else field.Append(c);
            }
            else if (c == '"') quoted = true;
            else if (c == ',') { row.Add(field.ToString()); field.Clear(); }
            else if (c == '\n') { row.Add(field.ToString()); field.Clear(); rows.Add(row); row = []; }
            else if (c != '\r') field.Append(c);
        }

        if (field.Length > 0 || row.Count > 0) { row.Add(field.ToString()); rows.Add(row); }
        return rows;
    }

    private static List<CsvTag> ReadSeed()
    {
        var rows = ParseCsv(File.ReadAllText(FindSeedCsv()));
        var header = rows[0];
        int Col(string name) => header.IndexOf(name);
        return [.. rows.Skip(1).Where(r => r.Count > 1).Select(r => new CsvTag(
            r[Col("Tag")], r[Col("LabelDe")], r[Col("LabelEn")], r[Col("Upos")], r[Col("AllowedUpos")].Split('|', StringSplitOptions.RemoveEmptyEntries)))];
    }

    private static IEnumerable<SttsTag> AsTable(IEnumerable<CsvTag> tags) =>
        tags.Select(t => new SttsTag { Tag = t.Tag, LabelDe = t.LabelDe, LabelEn = t.LabelEn, Upos = t.Upos, AllowedUpos = t.AllowedUpos });

    private static IReadOnlyList<ValueLabel> InCode => ElbLemmaValueMaps.Get(ElbLemmaValueMaps.Stts).Values;

    [Fact]
    public void Seed_csv_and_the_list_in_code_agree_on_tags_and_labels()
    {
        var differences = SttsTagDrift.Compare(InCode, AsTable(ReadSeed()));

        differences.ShouldBeEmpty();
    }

    [Fact]
    public void Seed_has_the_54_tags_of_STTS_1999()
    {
        var seed = ReadSeed();

        seed.Count.ShouldBe(54);
        seed.Select(t => t.Tag).Distinct().Count().ShouldBe(54);
        InCode.Count.ShouldBe(54);
    }

    [Fact]
    public void Every_seed_tag_accepts_its_default_UD_tag_and_only_known_ones()
    {
        var known = ElbLemmaValueMaps.Get(ElbLemmaValueMaps.PartOfSpeech).Values.Select(v => v.Code).Append("PUNCT").ToHashSet();

        foreach (var tag in ReadSeed())
        {
            tag.AllowedUpos.ShouldContain(tag.Upos, $"{tag.Tag} must accept its own default");
            tag.AllowedUpos.ShouldAllBe(u => known.Contains(u), $"{tag.Tag} lists an unknown UD tag");
        }
    }

    [Fact]
    public void A_changed_label_is_reported()
    {
        var table = AsTable(ReadSeed()).Select(t => t.Tag == "NN" ? new SttsTag { Tag = t.Tag, LabelDe = "anders", LabelEn = t.LabelEn, Upos = t.Upos, AllowedUpos = t.AllowedUpos } : t);

        var differences = SttsTagDrift.Compare(InCode, table);

        differences.Count.ShouldBe(1);
        differences[0].ShouldStartWith("NN: labelDe differs");
    }

    [Fact]
    public void A_tag_missing_in_the_table_or_in_code_is_reported_by_name()
    {
        var withoutNn = AsTable(ReadSeed()).Where(t => t.Tag != "NN");
        var extra = AsTable(ReadSeed()).Append(new SttsTag { Tag = "ZZ", LabelDe = "x", LabelEn = "x", Upos = "X", AllowedUpos = ["X"] });

        SttsTagDrift.Compare(InCode, withoutNn).ShouldBe(["NN: only in code"]);
        SttsTagDrift.Compare(InCode, extra).ShouldBe(["ZZ: only in table"]);
    }

    [Theory]
    [InlineData("VERB", "Fin", null, "VVFIN")]
    [InlineData("VERB", "Inf", null, "VVINF")]
    [InlineData("VERB", "Part", null, "VVPP")]
    [InlineData("VERB", "Fin", "Imp", "VVIMP")]
    [InlineData("AUX", "Fin", "Ind", "VAFIN")]
    [InlineData("NOUN", null, null, "NN")]
    [InlineData("PROPN", null, null, "NE")]
    public void Proposal_follows_part_of_speech_and_features(string pos, string? verbForm, string? mood, string expected)
    {
        SttsProposal.Propose(pos, verbForm, mood).ShouldBe(expected);
    }

    [Fact]
    public void Every_proposal_is_a_seed_tag_that_accepts_the_part_of_speech()
    {
        var seed = ReadSeed().ToDictionary(t => t.Tag);

        foreach (var rule in SttsProposal.Rules)
        {
            seed.ShouldContainKey(rule.Stts);
            seed[rule.Stts].AllowedUpos.ShouldContain(rule.PartOfSpeech, $"{rule.Stts} for {rule.PartOfSpeech}");
        }
    }

    [Fact]
    public void ProposeIfMismatch_proposes_only_when_the_pair_is_not_allowed()
    {
        var allowed = ReadSeed().ToDictionary(t => t.Tag, t => t.AllowedUpos);
        bool IsAllowed(string tag, string pos) => allowed[tag].Contains(pos);

        SttsProposal.ProposeIfMismatch("NN", "NOUN", null, null, IsAllowed).ShouldBeNull();
        SttsProposal.ProposeIfMismatch("NN", "VERB", "Inf", null, IsAllowed).ShouldBe("VVINF");
        SttsProposal.ProposeIfMismatch(null, "VERB", null, null, IsAllowed).ShouldBeNull();
    }
}
