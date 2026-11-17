using UnshackledWord.Application.Features.LinkRoles;
using UnshackledWord.Domain.Models.BibleStructure;

namespace UnshackledWord.Application.Features.AgentMapping.Scoring;

/// <summary>A verse of the live database as <see cref="ReferenceVerse"/> plus the id maps that translate a proposal (real database ids) into positions.</summary>
public sealed class AgentVerseSnapshot
{
    public ReferenceVerse State { get; init; } = new();

    /// <summary>German word id to reading position.</summary>
    public Dictionary<int, int> PositionById { get; init; } = [];

    /// <summary>Source word id to (original verse, position).</summary>
    public Dictionary<int, (int Ref, int Pos)> SourceById { get; init; } = [];

    /// <summary>Hebrew source word id to its main morpheme; absent for Greek and words without morphemes.</summary>
    public Dictionary<int, string> MainMorphemeById { get; init; } = [];

    /// <summary>Things worth a look before the verse becomes a reference (words without lemma, unlinked words).</summary>
    public List<string> Warnings { get; init; } = [];
}

/// <summary>Reads one verse out of the range data the package builder uses (<see cref="IAgentDataRepository.GetRangeAsync"/>).</summary>
public static class AgentVerseSnapshots
{
    public static string LanguageOf(int bibleBookId) => bibleBookId <= 39 ? "hebrew" : "greek";

    public static string RefText(int bibleBookId, int chapter, int verse) =>
        $"{(BibleBook.AllBooks.TryGetValue(bibleBookId, out var book) ? book.Name : bibleBookId.ToString())} {chapter}:{verse}";

    public static AgentVerseSnapshot Build(int bibleBookId, int chapter, int verse, AgentRangeData data)
    {
        var language = LanguageOf(bibleBookId);
        var german = data.GermanWords.Where(w => w.Chapter == chapter && w.Verse == verse).OrderBy(w => w.PositionInVerse).ToList();
        var ids = german.Select(w => w.Id).ToHashSet();
        var sources = data.SourceWords.ToDictionary(s => s.Id);
        var name = RefText(bibleBookId, chapter, verse);
        var warnings = new List<string>();

        var snapshot = new AgentVerseSnapshot
        {
            State = new ReferenceVerse { Book = bibleBookId, Chapter = chapter, Verse = verse, Language = language },
        };
        foreach (var word in german)
        {
            snapshot.PositionById[word.Id] = word.PositionInVerse;
        }

        if (german.Count == 0)
        {
            warnings.Add($"{name}: the verse has no German words.");
            snapshot.Warnings.AddRange(warnings);

            return snapshot;
        }

        if (german.Select(w => w.PositionInVerse).Distinct().Count() != german.Count)
        {
            warnings.Add($"{name}: two German words share a position; the verse cannot be keyed by position.");
        }

        snapshot.State.Text = string.Join(' ', german.Select(w => w.Text));
        foreach (var word in german.Where(w => w.Plain.Length > 0))
        {
            snapshot.State.Words.Add(new ReferenceWord { Pos = word.PositionInVerse, Text = word.Text, Lemma = word.Lemma, Stts = word.Stts });
            if (string.IsNullOrWhiteSpace(word.Lemma))
            {
                warnings.Add($"{name}: word {word.PositionInVerse} '{word.Text}' has no lemma.");
            }
        }

        foreach (var row in data.Mappings.Where(m => ids.Contains(m.ElbWordId)).OrderBy(m => snapshot.PositionById[m.ElbWordId]).ThenBy(m => m.PartOrder))
        {
            var g = snapshot.PositionById[row.ElbWordId];
            if (row.IsAddedWord)
            {
                if (row.ParentElbWordId is { } parent && snapshot.PositionById.TryGetValue(parent, out var parentPos))
                {
                    snapshot.State.Added.Add(new ReferenceAdded { G = g, Parent = parentPos });
                }

                continue;
            }

            if (row.StepWordId is not { } stepId || !sources.TryGetValue(stepId, out var source))
            {
                warnings.Add($"{name}: link of word {g} points to a source word outside the verse.");
                continue;
            }

            snapshot.SourceById[stepId] = (source.RefId, source.Position);
            var main = MainMorpheme(source, language);
            if (main is not null)
            {
                snapshot.MainMorphemeById[stepId] = main;
            }

            snapshot.State.Links.Add(new ReferenceLink
            {
                G = g,
                SRef = source.RefId,
                S = source.Position,
                SText = source.Text,
                Role = string.IsNullOrEmpty(row.Role) ? "head" : row.Role,
                Morpheme = ResolveMorpheme(language, row.StrongsNumber, main),
                Part = row.PartOrder,
                PartText = row.GermanWordPart,
                PartLemma = row.PartLemma,
                MultiHead = row.MultiHead ? true : null,
            });
        }

        // Pre-fill source lookups of the verse words that no link names (a proposal may link them).
        foreach (var source in data.SourceWords)
        {
            snapshot.SourceById.TryAdd(source.Id, (source.RefId, source.Position));
            if (!snapshot.MainMorphemeById.ContainsKey(source.Id) && MainMorpheme(source, language) is { } main)
            {
                snapshot.MainMorphemeById[source.Id] = main;
            }
        }

        var linked = snapshot.State.Links.Select(l => l.G).Concat(snapshot.State.Added.Select(a => a.G)).ToHashSet();
        foreach (var word in snapshot.State.Words.Where(w => !linked.Contains(w.Pos)))
        {
            warnings.Add($"{name}: word {word.Pos} '{word.Text}' has no link and is not an added word (fine for an untranslated article or particle, check it).");
        }

        snapshot.Warnings.AddRange(warnings);

        return snapshot;
    }

    /// <summary>A Hebrew link without its own morpheme renders the main morpheme; the reference spells it out so both spellings compare equal.</summary>
    public static string? ResolveMorpheme(string language, string? stored, string? main)
    {
        if (language != "hebrew")
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(stored) ? main : SourceMorphemes.NormalizeStrongs(stored);
    }

    private static string? MainMorpheme(AgentSourceWordRow source, string language) =>
        language == "hebrew" ? SourceMorphemes.ParseHebrew(source.ExpandedStrongTags).FirstOrDefault(m => m.IsMain)?.Strongs : null;
}
