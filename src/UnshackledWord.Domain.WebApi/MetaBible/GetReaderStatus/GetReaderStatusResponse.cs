namespace UnshackledWord.Domain.WebApi.MetaBible.GetReaderStatus;

public sealed class GetReaderStatusResponse
{
    /// <summary>False on a database that has not imported any Elb1871 text yet (roadmap 0031).</summary>
    public bool HasText { get; set; }
}
