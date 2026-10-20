using System.ComponentModel;
using System.Text;
using ModelContextProtocol.Server;
using UnshackledWord.Application.Repositories;
using UnshackledWord.Domain.Models.Dbo.Step;
using UnshackledWord.Domain.Models.Study;

namespace UnshackledWord.Tooling.McpServer.Tools;

[McpServerToolType]
public static class LexiconTools
{
    public const int ShortLexiconLength = 800;

    [McpServerTool(Name = "lookup_strongs", ReadOnly = true)]
    [Description("Looks up a Strong's number in the STEP Bible lexicon: original word, transliteration, morphology, English gloss and lexicon text. Without a letter suffix all variants (e.g. H1254A, H1254B) are listed.")]
    public static async Task<string> LookupStrongs(
        IStrongsLexiconRepository lexicon,
        [Description("Strong's number with language prefix, e.g. 'G26' (Greek), 'H1254' or 'H1254A' (Hebrew/Aramaic).")] string strongs,
        [Description("Return the complete lexicon text instead of the first 800 characters. Default false.")] bool full = false,
        CancellationToken cancellationToken = default)
    {
        var parsed = StrongsNumber.Parse(strongs);
        if (parsed.Value is null)
        {
            return parsed.Error!;
        }

        var entries = await lexicon.GetEntriesAsync(parsed.Value.Value, cancellationToken);
        if (entries.Count == 0)
        {
            return $"No lexicon entry found for '{strongs}'.";
        }

        var sb = new StringBuilder();
        foreach (var entry in entries)
        {
            var language = (StrongsLanguage)entry.LanguageId;
            var prefix = language == StrongsLanguage.Greek ? 'G' : 'H';
            sb.AppendLine($"{prefix}{entry.Number:D4}{entry.Extra} ({language}) {entry.OriginalWord} [{entry.Transliteration}]");
            sb.AppendLine($"Gloss: {entry.Gloss}");
            sb.AppendLine($"Morphology: {entry.Morphology}");
            if (!string.IsNullOrWhiteSpace(entry.DisambiguatedExtra))
            {
                sb.AppendLine($"Disambiguation: {entry.DisambiguatedExtra}");
            }

            if (!string.IsNullOrWhiteSpace(entry.Lexicon))
            {
                if (full || entry.Lexicon.Length <= ShortLexiconLength)
                {
                    sb.AppendLine($"Lexicon: {entry.Lexicon}");
                }
                else
                {
                    sb.AppendLine($"Lexicon: {entry.Lexicon[..ShortLexiconLength]}…");
                    sb.AppendLine("(Lexicon text shortened. Call again with full=true for the complete text.)");
                }
            }

            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }
}
