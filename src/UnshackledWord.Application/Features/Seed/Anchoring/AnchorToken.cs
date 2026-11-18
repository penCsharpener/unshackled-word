namespace UnshackledWord.Application.Features.Seed.Anchoring;

/// <summary>
/// One word of a chapter as the anchoring sees it: where it is (<see cref="Ref"/> = <c>HebRefId</c> <c>BBCCCVVV</c> for German
/// words, the STEP <c>LxxRefId</c> for source words, and the position in that verse) and what it is
/// (<see cref="Text"/> = the normalised token, see <see cref="TokenKey"/>, or the <c>SrcHash</c> of a STEP word).
/// Contains no database id, so tokens of an old and a new installation can be compared.
/// </summary>
public readonly record struct AnchorToken(int Ref, int Pos, string Text)
{
    /// <summary>Book and chapter of the token (<c>Ref / 1000</c>): the unit the aligner works on.</summary>
    public int Chapter => ChapterOf(Ref);

    public static int ChapterOf(int reference) => reference / 1000;

    /// <summary>Creates the token of a German word; the raw word is normalised with <see cref="TokenKey.Normalize"/>.</summary>
    public static AnchorToken ForWord(int reference, int position, string? word)
    {
        return new AnchorToken(reference, position, TokenKey.Normalize(word));
    }

    /// <summary>
    /// A bare dash between two words (<c>Sohn - seine</c>, produced by the hyphen repair of the base text). It is a word of
    /// <c>Elb1871Words</c> so positions stay complete, but it has no Greek or Hebrew original and gets no anchor token.
    /// </summary>
    public static bool IsBareDash(string? wordInContext) => wordInContext?.Trim() == "-";

    public override string ToString() => $"{Ref}:{Pos} {Text}";
}
