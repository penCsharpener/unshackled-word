using System.Security.Claims;
using FastEndpoints;
using UnshackledWord.Domain.WebApi.Vocab;
using UnshackledWord.Infrastructure.Vocab;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Vocab.GetCourses;

/// <summary>Anonymous: the vocab courses with their entry and occurrence totals. Subject to the general anonymous rate limit.</summary>
public sealed class Endpoint : EndpointWithoutRequest<GetVocabCoursesResponse>
{
    private readonly VocabCourseRepository _repository;
    private readonly VocabChangeService _changes;

    public Endpoint(VocabCourseRepository repository, VocabChangeService changes)
    {
        _repository = repository;
        _changes = changes;
    }

    public override void Configure()
    {
        Get("courses");
        Group<RouteGroupConfig>();
        AllowAnonymous();
        Summary(s => s.Summary = "The vocab courses (code, track, names, total entries and total occurrences without morphemes).");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        int? userId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
        var response = new GetVocabCoursesResponse { Courses = await _repository.GetCoursesAsync(userId) };

        if (userId is not null)
        {
            response.ChangedCount = await _changes.CountAsync(userId.Value, ct);
        }

        await Send.OkAsync(response, ct);
    }
}
