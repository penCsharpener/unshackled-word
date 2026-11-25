namespace UnshackledWord.Domain.WebApi.GreekEditions.GetGreekEditions;

/// <summary>The catalog of Greek text editions (roadmap 0025), ordered by catalog sort. Null properties are omitted by the serializer.</summary>
public sealed class GetGreekEditionsResponse
{
    public List<GreekEditionResponse> Editions { get; set; } = [];
}

public sealed class GreekEditionResponse
{
    /// <summary>Token as in STEP: NA28, TR, P66*.</summary>
    public string Code { get; set; } = default!;

    /// <summary>German name.</summary>
    public string Name { get; set; } = default!;
    public int? Year { get; set; }

    /// <summary>edition, translation, manuscript, version or unknown.</summary>
    public string Kind { get; set; } = default!;

    /// <summary>One of the eight printed main editions; only these count for "in all editions".</summary>
    public bool IsMain { get; set; }
}
