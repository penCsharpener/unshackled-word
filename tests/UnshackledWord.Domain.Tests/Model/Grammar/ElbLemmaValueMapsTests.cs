using UnshackledWord.Domain.Models.Grammar;

namespace UnshackledWord.Domain.Tests.Model.Grammar;

public class ElbLemmaValueMapsTests
{
    // Distinct non-null values observed in "Elb1871Morphology" of the dev DB (2026-11). Extend when new ones appear.
    private static readonly Dictionary<string, string[]> Observed = new()
    {
        [ElbLemmaValueMaps.PartOfSpeech] = ["ADJ", "ADP", "ADV", "AUX", "CCONJ", "DET", "INTJ", "NOUN", "NUM", "PART", "PRON", "PROPN", "SCONJ", "VERB", "X"],
        [ElbLemmaValueMaps.Degree] = ["Pos", "Cmp", "Sup"],
        [ElbLemmaValueMaps.VerbForm] = ["Fin", "Inf", "Part"],
        [ElbLemmaValueMaps.Tense] = ["Pres", "Past"], // observed in DB; Perf/Pqp/Fut/FutPerf are added for editing
        // (see Extra_tenses_are_mapped)
        [ElbLemmaValueMaps.Person] = ["1", "2", "3"],
        [ElbLemmaValueMaps.Number] = ["S", "P"],
        [ElbLemmaValueMaps.Mood] = ["Ind", "Sub", "Imp"],
        [ElbLemmaValueMaps.Case] = ["Nom", "Gen", "Dat", "Acc"],
        [ElbLemmaValueMaps.Gender] = ["M", "F", "N"],
        [ElbLemmaValueMaps.Stts] =
        [
            "NN", "ART", "KON", "PPER", "VVFIN", "APPR", "VAFIN", "NE", "PPOSAT", "PTKVZ", "ADV", "VVPP", "VVINF", "ADJA", "KOUS",
            "PIS", "ADJD", "PTKNEG", "VMFIN", "APPRART", "KOKOM", "CARD", "PDAT", "PRF", "PWS", "PROAV", "VAINF", "VVIMP", "PIAT",
            "PWAT", "PDS", "PWAV", "FM", "VAPP", "APZR", "VVIZU", "XY", "KOUI", "PTKANT", "APPO", "ITJ", "VMPP", "PTKA",
        ],
    };

    public static IEnumerable<object[]> Fields() => Observed.Keys.Select(k => new object[] { k });

    [Theory]
    [MemberData(nameof(Fields))]
    public void Every_observed_code_has_both_labels(string field)
    {
        var map = ElbLemmaValueMaps.Get(field);
        foreach (var code in Observed[field])
        {
            var entry = map.Values.SingleOrDefault(v => v.Code == code);
            entry.ShouldNotBeNull($"{field}:{code} is not mapped");
            entry.LabelDe.ShouldNotBeNullOrWhiteSpace();
            entry.LabelEn.ShouldNotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public void Every_field_is_covered_and_codes_are_unique()
    {
        ElbLemmaValueMaps.All.Select(f => f.Field).ShouldBe(Observed.Keys, ignoreOrder: true);
        foreach (var f in ElbLemmaValueMaps.All)
            f.Values.Select(v => v.Code).ShouldBeUnique();
    }

    [Fact]
    public void Part_of_speech_labels_are_lower_case()
    {
        foreach (var v in ElbLemmaValueMaps.Get(ElbLemmaValueMaps.PartOfSpeech).Values)
        {
            v.LabelDe.ShouldBe(v.LabelDe.ToLowerInvariant());
            v.LabelEn.ShouldBe(v.LabelEn.ToLowerInvariant());
        }
    }

    [Fact]
    public void Extra_tenses_are_mapped()
    {
        foreach (var code in new[] { "Perf", "Pqp", "Fut", "FutPerf" })
            ElbLemmaValueMaps.IsValid(ElbLemmaValueMaps.Tense, code).ShouldBeTrue(code);
    }

    [Fact]
    public void Rules_cover_every_part_of_speech_once_with_known_fields()
    {
        var pos = ElbLemmaValueMaps.Get(ElbLemmaValueMaps.PartOfSpeech).Values.Select(v => v.Code);
        ElbLemmaValueMaps.Rules.Select(r => r.PartOfSpeech).ShouldBe(pos, ignoreOrder: true);

        var fields = ElbLemmaValueMaps.All.Select(f => f.Field).ToHashSet();
        var verbForms = ElbLemmaValueMaps.Get(ElbLemmaValueMaps.VerbForm).Values.Select(v => v.Code).ToList();
        foreach (var rule in ElbLemmaValueMaps.Rules)
        {
            rule.Fields.ShouldAllBe(f => fields.Contains(f) && f != ElbLemmaValueMaps.PartOfSpeech && f != ElbLemmaValueMaps.Stts);
            if (rule.ByVerbForm is null) continue;
            rule.ByVerbForm.Select(v => v.VerbForm).ShouldBe(verbForms, ignoreOrder: true);
            rule.ByVerbForm.SelectMany(v => v.Fields).ShouldAllBe(f => fields.Contains(f));
        }
    }

    [Fact]
    public void Verb_and_noun_rules_match_the_decision()
    {
        var verb = ElbLemmaValueMaps.Rules.Single(r => r.PartOfSpeech == "VERB");
        verb.Fields.ShouldBe(["verbForm"]);
        verb.ByVerbForm!.Single(v => v.VerbForm == "Fin").Fields.ShouldBe(["tense", "person", "number", "mood"]);
        verb.ByVerbForm!.Single(v => v.VerbForm == "Part").Fields.ShouldBe(["tense", "case", "gender", "number", "degree"], ignoreOrder: true);
        ElbLemmaValueMaps.Rules.Single(r => r.PartOfSpeech == "NOUN").Fields.ShouldBe(["number", "case", "gender"]);
        ElbLemmaValueMaps.Rules.Single(r => r.PartOfSpeech == "ADP").Fields.ShouldBeEmpty();
    }
}
