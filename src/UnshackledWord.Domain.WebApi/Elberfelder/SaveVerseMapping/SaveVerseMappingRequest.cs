using UnshackledWord.Domain.WebApi.Elberfelder.GetVerseMapping;

namespace UnshackledWord.Domain.WebApi.Elberfelder.SaveVerseMapping;

public sealed class SaveVerseMappingRequest
{
    public int BibleBookId { get; set; }
    public int ChapterId { get; set; }
    public int VerseId { get; set; }

    /// <summary>The complete new mapping of the verse; it replaces all existing rows. Unlinked words have no entry.</summary>
    public List<VerseMappingLink> Links { get; set; } = [];
}
