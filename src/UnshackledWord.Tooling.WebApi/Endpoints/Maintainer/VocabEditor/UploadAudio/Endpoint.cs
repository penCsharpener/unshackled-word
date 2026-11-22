using FastEndpoints;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Infrastructure.Vocab;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.VocabEditor.UploadAudio;

public sealed class UploadAudioRequest
{
    public int Id { get; set; }

    public IFormFile File { get; set; } = null!;
}

/// <summary>Stores the MP3 pronunciation of an entry (roadmap 0022.6): at most 1 MB, checked by magic bytes, new object key per upload, old object removed afterwards. No content version bump.</summary>
[RequiresLogin]
public sealed class Endpoint : VocabEditEndpoint<UploadAudioRequest>
{
    private readonly IServiceProvider _services;

    public Endpoint(IServiceProvider services)
    {
        _services = services;
    }

    public override void Configure()
    {
        Put("vocab/entries/{id}/audio");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        AllowFileUploads();
        Summary(s => s.Summary = "Uploads the MP3 pronunciation of an entry (multipart field `file`, at most 1 MB, MP3 magic bytes checked). Replaces an existing one under a new object key. 400 for a wrong format or size, 404 for an unknown entry, 503 while object storage is not configured. Does not bump the content version.");
    }

    public override async Task HandleAsync(UploadAudioRequest req, CancellationToken ct)
    {
        var storage = _services.GetService<IObjectStorage>();

        if (storage is null)
        {
            AddError("Object storage is not configured.");
            await Send.ErrorsAsync(StatusCodes.Status503ServiceUnavailable, ct);

            return;
        }

        if (req.File.Length > VocabAudio.MaxBytes)
        {
            AddError($"The file is larger than {VocabAudio.MaxBytes / 1024} KB.");
            await Send.ErrorsAsync(StatusCodes.Status400BadRequest, ct);

            return;
        }

        using var buffer = new MemoryStream();
        await using (var stream = req.File.OpenReadStream())
        {
            await stream.CopyToAsync(buffer, ct);
        }

        var service = new VocabAudioService(_services.GetRequiredService<IDbConnectionFactory>(), storage);
        await SendOutcomeAsync(await service.UploadAsync(req.Id, buffer.ToArray(), UserId, ct), ct);
    }
}
