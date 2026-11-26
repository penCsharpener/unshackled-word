using UnshackledWord.Application.Features.GrammarSearch;

namespace UnshackledWord.Application.Tests.Features.GrammarSearch;

public class DistinctWordAssignmentTests
{
    [Fact]
    public void TwoSlotsOnTheSameSingleWord_CannotBeAssigned() =>
        DistinctWordAssignment.Exists([[1], [1]]).ShouldBeFalse();

    [Fact]
    public void SharedCandidates_AreResolvedByTheOtherWord() =>
        DistinctWordAssignment.Exists([[1, 2], [1]]).ShouldBeTrue();

    [Fact]
    public void ThreeSlotsOverTwoWords_CannotBeAssigned() =>
        DistinctWordAssignment.Exists([[1, 2], [1, 2], [1, 2]]).ShouldBeFalse();

    [Fact]
    public void DisjointSlots_AreFine() =>
        DistinctWordAssignment.Exists([[1], [2], [3]]).ShouldBeTrue();
}
