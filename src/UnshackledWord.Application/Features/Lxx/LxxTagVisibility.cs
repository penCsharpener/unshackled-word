namespace UnshackledWord.Application.Features.Lxx;

/// <summary>
/// Who sees which LXX tag (roadmap 0028 decision "public reads show only confirmed tags"): everyone sees <c>confirmed</c>, only Admin/Maintainer
/// (<paramref name="includeProposed"/>) also see <c>proposed</c>. A learning tool that teaches a wrong parse is worse than one that shows none.
/// </summary>
public static class LxxTagVisibility
{
    public const string Confirmed = "confirmed";
    public const string Proposed = "proposed";

    public static bool IsVisible(string? status, bool includeProposed) =>
        status == Confirmed || (includeProposed && status == Proposed);
}
