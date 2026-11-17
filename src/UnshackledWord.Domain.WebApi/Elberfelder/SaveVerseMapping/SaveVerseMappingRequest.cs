using UnshackledWord.Domain.WebApi.Elberfelder.GetVerseMapping;

namespace UnshackledWord.Domain.WebApi.Elberfelder.SaveVerseMapping;

public sealed class SaveVerseMappingRequest
{
    public int BibleBookId { get; set; }
    public int ChapterId { get; set; }
    public int VerseId { get; set; }

    /// <summary>The complete new mapping of the verse; it replaces all existing rows. Unlinked words have no entry.</summary>
    public List<VerseMappingLink> Links { get; set; } = [];

    /// <summary>
    /// A save that would remove compound parts the verse has today (a German word with parts that comes back without them) is rejected
    /// unless this is true. Protects the parts from a client that does not know them.
    /// </summary>
    public bool ClearParts { get; set; }
}
