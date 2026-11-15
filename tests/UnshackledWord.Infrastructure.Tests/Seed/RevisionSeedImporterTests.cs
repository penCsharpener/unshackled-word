using UnshackledWord.Application.Features.Seed.Revision;
using UnshackledWord.Infrastructure.Seed;
using UnshackledWord.Infrastructure.Seed.Revision;

namespace UnshackledWord.Infrastructure.Tests.Seed;

public class RevisionSeedImporterTests
{
    private static readonly (int Ref, int Level) A = (43003016, 200);
    private static readonly (int Ref, int Level) B = (43003017, 200);
    private static readonly (int Ref, int Level) C = (43003018, 200);

    [Fact]
    public void Decide_ShouldReplaceUntouchedVariants_KeepLocalOnes_AndAdoptEqualOnes()
    {
        var desired = new Dictionary<(int, int), string> { [A] = "new-a", [B] = "new-b", [C] = "same" };
        var state = new Dictionary<(int, int), string> { [A] = "old-a", [B] = "old-b", [C] = "same" };
        var database = new Dictionary<(int, int), string> { [A] = "old-a", [B] = "edited-locally", [C] = "same" };

        var outcomes = RevisionSeedImporter.Decide(desired, state, database);

        outcomes[A].ShouldBe(RevisionOutcome.Replaced);
        outcomes[B].ShouldBe(RevisionOutcome.KeptLocal);
        outcomes[C].ShouldBe(RevisionOutcome.Unchanged);
    }

    [Fact]
    public void Decide_ShouldAddNewVariants_EndWithdrawnOnes_AndKeepAVariantWithdrawnLocally()
    {
        var desired = new Dictionary<(int, int), string> { [A] = "a", [C] = "c" };
        var state = new Dictionary<(int, int), string> { [B] = "b", [C] = "c" };
        var database = new Dictionary<(int, int), string> { [B] = "b" };

        var outcomes = RevisionSeedImporter.Decide(desired, state, database);

        outcomes[A].ShouldBe(RevisionOutcome.New);
        outcomes[B].ShouldBe(RevisionOutcome.Removed);
        outcomes[C].ShouldBe(RevisionOutcome.KeptLocal); // in state and seed, but gone from the database: removed locally on purpose
    }

    [Fact]
    public void Decide_ShouldIgnoreAVariantTheSeedAndTheDatabaseBothLack()
    {
        var outcomes = RevisionSeedImporter.Decide(new Dictionary<(int, int), string>(), new Dictionary<(int, int), string>(), new Dictionary<(int, int), string>());

        outcomes.ShouldBeEmpty();
    }

    [Fact]
    public void Decide_ShouldKeepEverythingLocal_WhenAVariantWasCreatedLocallyBeforeTheSeedHadIt()
    {
        var outcomes = RevisionSeedImporter.Decide(
            new Dictionary<(int, int), string> { [A] = "seed" },
            new Dictionary<(int, int), string>(),
            new Dictionary<(int, int), string> { [A] = "local" });

        outcomes[A].ShouldBe(RevisionOutcome.KeptLocal);
    }

    [Fact]
    public void Serialize_ShouldWriteTheRevisionHeaders_AndReadBackTheSameLines()
    {
        var verses = new List<RevisionVerseSeedLine>
        {
            new() { Ref = 43003016, Level = 200, BaseHash = "abcdef123456", Text = "Denn also hat Gott, die Welt geliebt", Note = "Test, \"Note\"", Reviewed = true },
            new() { Ref = 43003016, Level = 600, BasedOnLevel = 200, BaseHash = "abcdef123456", Text = "Gott liebt die Welt so sehr" }
        };
        var mapping = new List<RevisionMappingSeedLine>
        {
            new() { Ref = 43003016, Level = 200, Pos = 3, Form = "hat", SrcRef = 43003016, SrcPos = 3, SrcHash = "aaaa1111", Origin = "carried" },
            new() { Ref = 43003016, Level = 200, Pos = 1, Form = "Denn", Added = true, ParentPos = 2, ParentForm = "also", Origin = "manual" }
        };

        var (verseBytes, _) = SeedCsv.Serialize<RevisionVerseSeedLine, RevisionVerseSeedLineMap>(verses);
        var (mappingBytes, _) = SeedCsv.Serialize<RevisionMappingSeedLine, RevisionMappingSeedLineMap>(mapping);

        System.Text.Encoding.UTF8.GetString(verseBytes).Split('\n')[0].ShouldBe("Ref,Level,BasedOnLevel,BaseHash,Text,Note,Reviewed");
        System.Text.Encoding.UTF8.GetString(mappingBytes).Split('\n')[0].ShouldBe("Ref,Level,Pos,Form,Part,SrcRef,SrcPos,SrcHash,Added,ParentPos,ParentForm,Origin");

        var readVerses = SeedCsv.Read<RevisionVerseSeedLine, RevisionVerseSeedLineMap>(verseBytes);
        readVerses.Count.ShouldBe(2);
        readVerses[0].Note.ShouldBe("Test, \"Note\"");
        readVerses[0].Reviewed.ShouldBeTrue();
        readVerses[1].BasedOnLevel.ShouldBe(200);

        var readMapping = SeedCsv.Read<RevisionMappingSeedLine, RevisionMappingSeedLineMap>(mappingBytes);
        readMapping[1].Added.ShouldBeTrue();
        readMapping[1].ParentPos.ShouldBe(2);
        readMapping[0].Source!.Value.SrcHash.ShouldBe("aaaa1111");
    }

    [Fact]
    public void RevisionPaths_ShouldBeBelowTheSeedRootWithForwardSlashes()
    {
        SeedLayout.RevisionPath(SeedLayout.RevisionVersesFolder, SeedLayout.BookFileName(43)).ShouldBe("revision/verses/43-John.csv");
        SeedLayout.RevisionPath(null, SeedLayout.RevisionLevelsFile).ShouldBe("revision/levels.csv");
    }
}
