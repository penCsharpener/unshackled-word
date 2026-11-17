using UnshackledWord.Domain.WebApi.Elberfelder.GetVerseMapping;

namespace UnshackledWord.Application.Features.VerseMapping;

/// <summary>Link roles of roadmap 0018 (04-roles-and-counting.md) and the helpers the save paths share.</summary>
public static class MappingRoles
{
    /// <summary>Carries the meaning of the source morpheme; counted.</summary>
    public const string Head = "head";

    /// <summary>Belongs to the head's German form (<c>habe</c> in <c>habe getauft</c>); not counted.</summary>
    public const string Unit = "unit";

    public const int MaxPartLemmaLength = 40;

    /// <summary>STTS tag of a preposition with article (<c>zum</c>, <c>im</c>, <c>vom</c>).</summary>
    public const string ContractionStts = "APPRART";

    /// <summary>Greek article ὁ.</summary>
    public const string GreekArticle = "G3588";

    public static bool IsKnown(string? role) => role is null or Head or Unit;

    public static bool IsUnit(VerseMappingLink link) => link.Role == Unit;

    /// <summary>
    /// True when the request knows roles: at least one link sets role, strongsNumber, partLemma or allowMultiHead. A request without any of
    /// them comes from a client that predates roadmap 0018; its save keeps the stored values and skips the one-head rule.
    /// </summary>
    public static bool IsRoleAware(IEnumerable<VerseMappingLink> links) =>
        links.Any(l => l.Role is not null || !string.IsNullOrWhiteSpace(l.StrongsNumber) || !string.IsNullOrWhiteSpace(l.PartLemma) || l.AllowMultiHead is not null);
}
