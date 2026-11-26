using System.Text.Json;

namespace UnshackledWord.Application.Features.AgentMapping;

/// <summary>
/// Reads <c>AgentVerseProposals.Flags</c> of either shape: mapping proposals store an array of flags; LXX proposals (roadmap 0028.7) an object
/// <c>{textForm, subverse, lxxVerseId, hebRefId?, flags: [...]}</c> (the open-proposal index reads textForm/subverse from it).
/// </summary>
public static class AgentProposalFlagsJson
{
    /// <summary>The flags array as JSON text, or null when there is none.</summary>
    public static string? Array(string? flagsJson)
    {
        if (string.IsNullOrWhiteSpace(flagsJson))
        {
            return null;
        }

        using var document = JsonDocument.Parse(flagsJson);
        var root = document.RootElement;
        return root.ValueKind switch
        {
            JsonValueKind.Array => root.GetRawText(),
            JsonValueKind.Object when root.TryGetProperty("flags", out var flags) && flags.ValueKind == JsonValueKind.Array => flags.GetRawText(),
            _ => null,
        };
    }

    /// <summary>The <c>code</c> of every flag.</summary>
    public static List<string> Codes(string? flagsJson)
    {
        if (Array(flagsJson) is not { } array)
        {
            return [];
        }

        using var document = JsonDocument.Parse(array);
        return document.RootElement.EnumerateArray()
            .Select(f => f.ValueKind == JsonValueKind.Object && f.TryGetProperty("code", out var code) && code.ValueKind == JsonValueKind.String ? code.GetString() : null)
            .Where(c => c is not null)
            .Cast<string>()
            .ToList();
    }
}
