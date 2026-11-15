namespace UnshackledWord.Application.Features.Revision;

/// <summary>Values of <c>RevisionMapping."Origin"</c>.</summary>
public static class RevisionOrigin
{
    public const string Carried = "carried";
    public const string Manual = "manual";
    public const string Seed = "seed";
}

/// <summary>Values of <c>RevisionVerseAudit."Action"</c> as the history read sees them.</summary>
public static class RevisionHistoryAction
{
    public const string Text = "text";
    public const string Mapping = "mapping";
    public const string Withdraw = "withdraw";
}
