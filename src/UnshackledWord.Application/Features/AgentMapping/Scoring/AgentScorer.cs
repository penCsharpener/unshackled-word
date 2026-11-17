using UnshackledWord.Domain.WebApi.Maintainer;

namespace UnshackledWord.Application.Features.AgentMapping.Scoring;

/// <summary>What the scorer knows about one reference verse of a run.</summary>
public sealed class ScoredVerseInput
{
    /// <summary>The reference verse the run is judged against.</summary>
    public ReferenceVerse Expected { get; init; } = new();

    /// <summary>The state the run leaves behind: stored state with the proposal applied (or the stored state alone, silence).</summary>
    public ReferenceVerse Actual { get; init; } = new();

    public bool HasProposal { get; init; }

    /// <summary>The verse was in a package of the run (the model was asked about it).</summary>
    public bool InPackage { get; init; }

    public IReadOnlyList<string> FlagCodes { get; init; } = [];
    public long InputTokens { get; init; }
    public long CachedInputTokens { get; init; }
    public long OutputTokens { get; init; }
    public decimal CostUsd { get; init; }
}

public sealed class AgentScoreInput
{
    public int RunId { get; init; }
    public string Mode { get; init; } = string.Empty;
    public string Profile { get; init; } = string.Empty;
    public string Model { get; init; } = string.Empty;
    public string PromptVersion { get; init; } = string.Empty;
    public string RunStatus { get; init; } = string.Empty;
    public string ReferenceVersion { get; init; } = string.Empty;
    public int ReferenceVerses { get; init; }
    public int NotInRun { get; init; }
    public int ExtraRunVerses { get; init; }
    public IReadOnlyList<ScoredVerseInput> Verses { get; init; } = [];
    public DateTimeOffset ComputedAt { get; init; }
}

/// <summary>
/// Compares what a run left behind with the maintainer's reference (roadmap 0018.10). Pure: everything it needs is in <see cref="AgentScoreInput"/>.
/// Lemma and STTS are scored per reference word that has a value, role and morpheme per reference link (a missing link counts as wrong), links
/// and added words as precision/recall.
/// </summary>
public static class AgentScorer
{
    /// <summary>Bar for a bulk merge (0018 02-decisions.md): lemmas, roles/morphemes, no hard-validator failure.</summary>
    public const double LemmaBar = 0.98;

    public const double RoleMorphemeBar = 0.97;

    public static AgentScoreResponse Score(AgentScoreInput input)
    {
        var response = new AgentScoreResponse
        {
            RunId = input.RunId,
            Mode = input.Mode,
            Profile = input.Profile,
            Model = input.Model,
            PromptVersion = input.PromptVersion,
            RunStatus = input.RunStatus,
            ReferenceVersion = input.ReferenceVersion,
            ComputedAt = input.ComputedAt,
        };

        int lemmaTotal = 0, lemmaOk = 0, sttsTotal = 0, sttsOk = 0, partTotal = 0, partOk = 0;
        int roleTotal = 0, roleOk = 0, morphTotal = 0, morphOk = 0, bothOk = 0;
        int linkExpected = 0, linkActual = 0, linkMatched = 0, addExpected = 0, addActual = 0, addMatched = 0;
        int flagTotal = 0, flaggedVerses = 0, proposalVerses = 0, costVerses = 0;
        long inputTokens = 0, cachedTokens = 0, outputTokens = 0;
        decimal cost = 0;

        foreach (var verse in input.Verses)
        {
            var name = AgentVerseSnapshots.RefText(verse.Expected.Book, verse.Expected.Chapter, verse.Expected.Verse);
            var result = new AgentScoreVerse
            {
                Ref = name,
                BibleBookId = verse.Expected.Book,
                Chapter = verse.Expected.Chapter,
                Verse = verse.Expected.Verse,
                Status = verse.HasProposal ? "proposal" : verse.InPackage ? "no-proposal" : "silent",
                Flags = [.. verse.FlagCodes],
                InputTokens = verse.InputTokens,
                OutputTokens = verse.OutputTokens,
                CostUsd = verse.CostUsd,
            };

            var actualWords = verse.Actual.Words.ToDictionary(w => w.Pos);
            foreach (var expected in verse.Expected.Words)
            {
                actualWords.TryGetValue(expected.Pos, out var actual);
                if (!string.IsNullOrEmpty(expected.Lemma))
                {
                    lemmaTotal++;
                    if (string.Equals(expected.Lemma, actual?.Lemma, StringComparison.Ordinal))
                    {
                        lemmaOk++;
                    }
                    else
                    {
                        result.LemmaErrors.Add(new AgentScoreError { Item = expected.Pos.ToString(), Text = expected.Text, Expected = expected.Lemma, Actual = actual?.Lemma });
                    }
                }

                if (!string.IsNullOrEmpty(expected.Stts))
                {
                    sttsTotal++;
                    if (string.Equals(expected.Stts, actual?.Stts, StringComparison.Ordinal))
                    {
                        sttsOk++;
                    }
                    else
                    {
                        result.LemmaErrors.Add(new AgentScoreError { Item = $"{expected.Pos} (STTS)", Text = expected.Text, Expected = expected.Stts, Actual = actual?.Stts });
                    }
                }
            }

            var actualLinks = verse.Actual.Links.GroupBy(l => l.Key).ToDictionary(g => g.Key, g => g.First());
            var expectedKeys = verse.Expected.Links.Select(l => l.Key).ToHashSet();

            foreach (var expected in verse.Expected.Links)
            {
                actualLinks.TryGetValue(expected.Key, out var actual);
                roleTotal++;
                var roleRight = actual is not null && actual.Role == expected.Role;
                var morphRight = true;
                if (expected.Morpheme is not null)
                {
                    morphTotal++;
                    morphRight = actual is not null && string.Equals(actual.Morpheme, expected.Morpheme, StringComparison.OrdinalIgnoreCase);
                    morphOk += morphRight ? 1 : 0;
                }

                roleOk += roleRight ? 1 : 0;
                bothOk += roleRight && morphRight ? 1 : 0;
                if (!roleRight || !morphRight)
                {
                    result.RoleErrors.Add(new AgentScoreError
                    {
                        Item = LinkName(expected),
                        Text = expected.SText,
                        Expected = Describe(expected),
                        Actual = actual is null ? null : Describe(actual),
                    });
                }

                if (!string.IsNullOrEmpty(expected.PartLemma))
                {
                    partTotal++;
                    if (actual is not null && string.Equals(actual.PartLemma, expected.PartLemma, StringComparison.Ordinal))
                    {
                        partOk++;
                    }
                    else
                    {
                        result.LemmaErrors.Add(new AgentScoreError { Item = $"{LinkName(expected)} (part lemma)", Text = expected.PartText, Expected = expected.PartLemma, Actual = actual?.PartLemma });
                    }
                }
            }

            foreach (var expected in verse.Expected.Links.Where(l => !verse.Actual.Links.Any(a => a.Key == l.Key)))
            {
                result.MissingLinks.Add(new AgentScoreError { Item = LinkName(expected), Text = expected.SText, Expected = Describe(expected) });
            }

            foreach (var extra in verse.Actual.Links.Where(a => !expectedKeys.Contains(a.Key)))
            {
                result.ExtraLinks.Add(new AgentScoreError { Item = LinkName(extra), Text = extra.SText, Actual = Describe(extra) });
            }

            linkExpected += verse.Expected.Links.Count;
            linkActual += verse.Actual.Links.Count;
            linkMatched += verse.Expected.Links.Count(l => actualLinks.ContainsKey(l.Key));

            var actualAdded = verse.Actual.Added.Select(a => a.Key).ToHashSet();
            addExpected += verse.Expected.Added.Count;
            addActual += actualAdded.Count;
            addMatched += verse.Expected.Added.Count(a => actualAdded.Contains(a.Key));
            foreach (var missing in verse.Expected.Added.Where(a => !actualAdded.Contains(a.Key)))
            {
                result.MissingLinks.Add(new AgentScoreError { Item = $"added {missing.G}", Expected = $"added word of {missing.Parent}" });
            }

            foreach (var extra in verse.Actual.Added.Where(a => verse.Expected.Added.All(e => e.Key != a.Key)))
            {
                result.ExtraLinks.Add(new AgentScoreError { Item = $"added {extra.G}", Actual = $"added word of {extra.Parent}" });
            }

            if (verse.HasProposal)
            {
                proposalVerses++;
                flagTotal += verse.FlagCodes.Count;
                flaggedVerses += verse.FlagCodes.Count > 0 ? 1 : 0;
                foreach (var code in verse.FlagCodes)
                {
                    response.Flags.ByCode[code] = response.Flags.ByCode.GetValueOrDefault(code) + 1;
                }
            }

            if (verse.InPackage)
            {
                costVerses++;
                inputTokens += verse.InputTokens;
                cachedTokens += verse.CachedInputTokens;
                outputTokens += verse.OutputTokens;
                cost += verse.CostUsd;
            }

            response.PerVerse.Add(result);
        }

        response.Verses = new AgentScoreVerseCounts
        {
            Reference = input.ReferenceVerses,
            Scored = input.Verses.Count,
            NotInRun = input.NotInRun,
            WithProposal = proposalVerses,
            Silent = input.Verses.Count(v => !v.HasProposal && !v.InPackage),
            HardRejections = input.Verses.Count(v => !v.HasProposal && v.InPackage),
            ExtraRunVerses = input.ExtraRunVerses,
        };
        response.Lemmas = Ratio(lemmaTotal, lemmaOk);
        response.Stts = Ratio(sttsTotal, sttsOk);
        response.PartLemmas = Ratio(partTotal, partOk);
        response.Roles = Ratio(roleTotal, roleOk);
        response.Morphemes = Ratio(morphTotal, morphOk);
        response.RoleMorphemes = Ratio(roleTotal, bothOk);
        response.Links = Overlap(linkExpected, linkActual, linkMatched);
        response.AddedWords = Overlap(addExpected, addActual, addMatched);
        response.Flags.Total = flagTotal;
        response.Flags.VersesFlagged = flaggedVerses;
        response.Flags.PerVerse = proposalVerses == 0 ? null : Math.Round((double)flagTotal / proposalVerses, 3);
        response.Cost = new AgentScoreCost
        {
            InputTokens = inputTokens,
            CachedInputTokens = cachedTokens,
            OutputTokens = outputTokens,
            CostUsd = cost,
            Verses = costVerses,
            InputTokensPerVerse = costVerses == 0 ? null : Math.Round((double)inputTokens / costVerses, 1),
            OutputTokensPerVerse = costVerses == 0 ? null : Math.Round((double)outputTokens / costVerses, 1),
            CostUsdPerVerse = costVerses == 0 ? null : Math.Round(cost / costVerses, 6),
        };
        response.BulkMerge = EvaluateBar(response);

        return response;
    }

    public static AgentScoreBulkMerge EvaluateBar(AgentScoreResponse score)
    {
        var bar = new AgentScoreBulkMerge();
        var scored = score.Verses.Scored;

        void Check(string code, double? value, double required, bool ok, string reason)
        {
            bar.Checks.Add(new AgentScoreCheck { Code = code, Value = value, Required = required, Ok = ok });
            if (!ok)
            {
                bar.Reasons.Add(reason);
            }
        }

        Check("scored-verses", scored, 1, scored > 0, "No reference verse is covered by the run, nothing was measured.");
        Check("lemmas", score.Lemmas.Accuracy, LemmaBar, Exact(score.Lemmas) is { } l && l >= LemmaBar, $"Lemma accuracy {Percent(score.Lemmas.Accuracy)} is below {LemmaBar:P0}.");
        Check("role-morphemes", score.RoleMorphemes.Accuracy, RoleMorphemeBar, Exact(score.RoleMorphemes) is { } r && r >= RoleMorphemeBar, $"Role/morpheme accuracy {Percent(score.RoleMorphemes.Accuracy)} is below {RoleMorphemeBar:P0}.");
        Check("hard-rejections", score.Verses.HardRejections, 0, score.Verses.HardRejections == 0, $"{score.Verses.HardRejections} verse(s) were asked but have no proposal (rejected by the validator or not answered).");
        bar.Meets = bar.Checks.All(c => c.Ok);

        return bar;
    }

    /// <summary>The bar compares the exact fraction, not the rounded figure of the response.</summary>
    private static double? Exact(AgentScoreRatio ratio) => ratio.Total == 0 ? null : (double)ratio.Correct / ratio.Total;

    private static string Percent(double? value) => value is null ? "n/a" : $"{value:P1}";

    private static string LinkName(ReferenceLink link) => $"{link.G}→{link.SRef}.{link.S}" + (link.Part is null ? string.Empty : $" part {link.Part}");

    private static string Describe(ReferenceLink link) =>
        string.Join(' ', new[] { link.Role, link.Morpheme }.Where(x => !string.IsNullOrEmpty(x)));

    private static AgentScoreRatio Ratio(int total, int correct) => new()
    {
        Total = total,
        Correct = correct,
        Accuracy = total == 0 ? null : Math.Round((double)correct / total, 4),
    };

    private static AgentScoreOverlap Overlap(int expected, int actual, int matched) => new()
    {
        Expected = expected,
        Actual = actual,
        Matched = matched,
        Precision = actual == 0 ? null : Math.Round((double)matched / actual, 4),
        Recall = expected == 0 ? null : Math.Round((double)matched / expected, 4),
    };
}
