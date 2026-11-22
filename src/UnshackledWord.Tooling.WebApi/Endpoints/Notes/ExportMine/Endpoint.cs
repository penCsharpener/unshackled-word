using FastEndpoints;
using UnshackledWord.Application.Features.Notes;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Notes.ExportMine;

/// <summary>Download of the caller's own notes in the export format of roadmap 0023 (<c>notes-YYYYMMDD-HHmmss.json</c>).</summary>
[RequiresLogin]
public sealed class Endpoint : NoteEndpoint<EmptyRequest, NoteExportFile>
{
    private readonly NoteExportService _export;

    public Endpoint(NoteExportService export)
    {
        _export = export;
    }

    public override void Configure()
    {
        Get("mine/export");
        Group<RouteGroupConfig>();
        Description(b => b.Produces<NoteExportFile>(200, "application/json"));
        Summary(s => s.Summary = "Downloads all own notes with their anchors as a JSON file (`notes-YYYYMMDD-HHmmss.json`, format `unshackled-word.notes`).");
    }

    public override async Task HandleAsync(EmptyRequest req, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var file = await _export.BuildAsync(UserId, AppVersion.Current, now, ct);
        HttpContext.Response.Headers.CacheControl = "private, no-store";
        await Send.BytesAsync(NoteExportService.Serialize(file), NoteExportService.FileName(now), "application/json", cancellation: ct);
    }
}
