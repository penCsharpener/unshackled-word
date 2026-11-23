using FastEndpoints;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Infrastructure.Notes;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.Notes.ListFiles;

[RequiresLogin]
public sealed class Endpoint : EndpointWithoutRequest<List<NoteFileInfo>>
{
    private readonly NoteFileService _files;

    public Endpoint(NoteFileService files)
    {
        _files = files;
    }

    public override void Configure()
    {
        Get("notes/files");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "The notes export files in `temp/notes`, newest first (`name`, `sizeBytes`, `modifiedAt`); the first one is the default import source. Empty when there are none.");
    }

    public override async Task HandleAsync(CancellationToken ct) => await Send.OkAsync(_files.ListFiles().ToList(), ct);
}
