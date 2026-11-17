using UnshackledWord.Application.Features.LinkRoles.Backfill;
using UnshackledWord.Domain.WebApi.Maintainer;

namespace UnshackledWord.Application.Tests.Features.LinkRoles;

public class LinkRoleBackfillServiceTests
{
    private static readonly IProgress<string> Silent = new Progress<string>();

    private static ILinkRoleBackfillRepository Repository(int activeRuns = 0)
    {
        var repository = Substitute.For<ILinkRoleBackfillRepository>();
        repository.LoadKnownLemmasAsync(default).ReturnsForAnyArgs(new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        repository.GetBookIdsAsync(default).ReturnsForAnyArgs([1]);
        repository.LoadBookAsync(1, default).ReturnsForAnyArgs(new LinkRoleBookData(1, "Genesis",
            [
                new LinkRoleWordRow { Id = 31, HebRefId = 1001003, PositionInVerse = 1, Text = "Und", Lemma = "und", Stts = "KON", Chapter = 1, Verse = 3 },
                new LinkRoleWordRow { Id = 71, HebRefId = 1001003, PositionInVerse = 2, Text = "aus", Lemma = "ausgehen", Stts = "PTKVZ", Chapter = 1, Verse = 3 },
            ],
            [],
            [new LinkRoleMappingRow { Id = 1, ElbWordId = 31, HebRefId = 1001003, StepWordId = 21, IsAddedWord = false }],
            [],
            [new LinkRoleSourceRow { Id = 21, Morphology = "Hc/Vqw3ms", MorphemeTags = "H9001=ו=&/{H0559=אָמַר=to say}" }]));
        repository.CountActiveAgentRunsAsync(default).ReturnsForAnyArgs(activeRuns);
        repository.ApplyAsync(default!, default!, default, default!, default).ReturnsForAnyArgs(new LinkRoleApplyResult(7, 1, 1));
        return repository;
    }

    [Fact]
    public async Task Dry_run_reports_and_writes_nothing()
    {
        var repository = Repository();

        var result = await new LinkRoleBackfillService(repository).RunAsync(LinkRoleJobModes.DryRun, 1, Silent, CancellationToken.None);

        (result.MappingRows, result.LemmaRows, result.ChangeSetId).ShouldBe((1, 1, null));
        result.Report!.Changed["hebrew"]["1"].ShouldBe(1);
        await repository.DidNotReceiveWithAnyArgs().ApplyAsync(default!, default!, default, default!, default);
    }

    [Fact]
    public async Task Apply_writes_the_planned_rows_as_one_change_set()
    {
        var repository = Repository();

        var result = await new LinkRoleBackfillService(repository).RunAsync(LinkRoleJobModes.Apply, 5, Silent, CancellationToken.None);

        result.ChangeSetId.ShouldBe(7);
        await repository.Received(1).ApplyAsync(
            Arg.Is<IReadOnlyList<MappingRowChange>>(m => m.Count == 1 && m[0].After.StrongsNumber == "H9001"),
            Arg.Is<IReadOnlyList<LemmaRowChange>>(l => l.Count == 1 && l[0].NewLemma == "aus"),
            5, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Apply_refuses_while_an_agent_run_is_active()
    {
        var repository = Repository(activeRuns: 1);
        var service = new LinkRoleBackfillService(repository);

        (await service.CheckBlockedAsync(CancellationToken.None)).ShouldNotBeNull();
        await Should.ThrowAsync<InvalidOperationException>(() => service.RunAsync(LinkRoleJobModes.Apply, 1, Silent, CancellationToken.None));
        await repository.DidNotReceiveWithAnyArgs().ApplyAsync(default!, default!, default, default!, default);
    }

    [Fact]
    public async Task Undo_without_an_applied_change_set_fails()
    {
        var repository = Repository();
        repository.GetAppliedAsync(default).ReturnsForAnyArgs((LinkRoleChangeSetInfo?)null);

        await Should.ThrowAsync<InvalidOperationException>(() => new LinkRoleBackfillService(repository).RunAsync(LinkRoleJobModes.Undo, 1, Silent, CancellationToken.None));
    }

    [Fact]
    public async Task Undo_restores_the_latest_applied_change_set()
    {
        var repository = Repository();
        repository.GetAppliedAsync(default).ReturnsForAnyArgs(new LinkRoleChangeSetInfo { Id = 7, MappingRows = 3, LemmaRows = 1 });
        repository.UndoAsync(7, 2, default).ReturnsForAnyArgs(new LinkRoleUndoResult(7, 8, 3, 0, 1, 0));

        var result = await new LinkRoleBackfillService(repository).RunAsync(LinkRoleJobModes.Undo, 2, Silent, CancellationToken.None);

        (result.ChangeSetId, result.UndoChangeSetId, result.MappingRows, result.LemmaRows).ShouldBe((7, 8, 3, 1));
        await repository.Received(1).UndoAsync(7, 2, Arg.Any<CancellationToken>());
    }
}
