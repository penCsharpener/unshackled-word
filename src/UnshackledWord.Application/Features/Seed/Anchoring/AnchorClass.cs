namespace UnshackledWord.Application.Features.Seed.Anchoring;

/// <summary>What happened to a token of the old side when it was aligned onto the new side of a chapter.</summary>
public enum AnchorClass
{
    /// <summary>Same reference, same position, same text: the key is unchanged.</summary>
    Exact,

    /// <summary>Same text found at another position or in a neighbouring verse of the same chapter.</summary>
    Shifted,

    /// <summary>The text differs, but the token sits alone in a 1:1 replace between equal neighbours (a typo fix).</summary>
    Modified,

    /// <summary>No counterpart on the new side. Never imported, always reported.</summary>
    Orphaned
}
