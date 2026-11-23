using FastEndpoints;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Infrastructure.Notes;
using UnshackledWord.Tooling.WebApi.Auth;
using UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.SeedAdmin;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.Notes.Import;

[RequiresLogin]
public sealed class Endpoint : Endpoint<ImportNotesRequest, SeedJobResponse>
{
    public const string Operation = "notesImport";

    private readonly SeedJobRunner _runner;
    private readonly IServiceScopeFactory _scopeFactory;

    public Endpoint(SeedJobRunner runner, IServiceScopeFactory scopeFactory)
    {
        _runner = runner;
        _scopeFactory = scopeFactory;
    }

    public override void Configure()
    {
        Post("notes/import");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "Starts the notes import in the background from `temp/notes/{fileName}` (newest file when `fileName` is empty). Upsert by note id (an existing note only when the file's change time is newer), author by e-mail (unknown author: skipped and listed), deletes nothing. Returns the job with status 202 (operation `notesImport`; `result.steps[0].counts` inserted/updated/unchanged/skipped, `lists.skipped` with reasons); poll GET maintainer/seed/status. 409 while another seed or notes job runs, 422 when the temp folder is not reachable.");
    }

    public override async Task HandleAsync(ImportNotesRequest req, CancellationToken ct)
    {
        using (var scope = _scopeFactory.CreateScope())
        {
            var unavailable = scope.ServiceProvider.GetRequiredService<NoteFileService>().CheckAvailable();

            if (unavailable is not null)
            {
                AddError(unavailable);
                await Send.ErrorsAsync(StatusCodes.Status422UnprocessableEntity, ct);

                return;
            }
        }

        var job = _runner.TryStart(Operation, "notes", async (progress, token) =>
        {
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<NoteFileService>();
            var step = await service.ImportAsync(req.FileName, progress, token);

            return new SeedJobResult { Steps = [step] };
        });

        if (job is null)
        {
            AddError("Another seed job or export is already running.");
            await Send.ErrorsAsync(StatusCodes.Status409Conflict, ct);

            return;
        }

        await Send.ResponseAsync(job, StatusCodes.Status202Accepted, ct);
    }
}
