using FastEndpoints;
using UnshackledWord.Domain.WebApi.Vocab;
using UnshackledWord.Infrastructure.Vocab;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Vocab.GetCourses;

/// <summary>Anonymous: the vocab courses with their entry and occurrence totals. Subject to the general anonymous rate limit.</summary>
public sealed class Endpoint : EndpointWithoutRequest<GetVocabCoursesResponse>
{
    private readonly VocabCourseRepository _repository;

    public Endpoint(VocabCourseRepository repository)
    {
        _repository = repository;
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
        await Send.OkAsync(new GetVocabCoursesResponse { Courses = await _repository.GetCoursesAsync() }, ct);
    }
}
