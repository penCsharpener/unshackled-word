using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Abstractions.Step;
using UnshackledWord.Domain.Models.Dbo.Step;
using UnshackledWord.Domain.Models.Extensions;
using UnshackledWord.Tooling.SeedDb.Services.Abstractions;

namespace UnshackledWord.Tooling.SeedDb.Services.StepBible.LexiconParser;

public sealed class StepPersonPlaceLexiconStrategy : IFileParserStrategy
{
    private readonly IFileService _fileService;
    private readonly IStepPersonPlaceRepository _repo;
    private readonly ILogger<StepPersonPlaceLexiconStrategy> _logger;
    private readonly StepLexiconStrategyFactory _factory;

    public StepPersonPlaceLexiconStrategy(IFileService fileService, IStepPersonPlaceRepository repo, ILogger<StepPersonPlaceLexiconStrategy> logger)
    {
        _fileService = fileService;
        _repo = repo;
        _logger = logger;
        _factory = new StepLexiconStrategyFactory();
    }

    private const string PersonSeparator = "========== PERSON(s)";
    private const string PlaceSeparator = "$========== PLACE";
    private const string OtherSeparator = "$========== OTHER";

    public async Task SaveToDatabase(string filePath, CancellationToken token = default)
    {
        var personCount = await _repo.CountPersonsByFilterAsync(new(), token);
        var relationCount = await _repo.CountPersonRelationsByFilterAsync(new(), token);
        var placeCount = await _repo.CountPlacesByFilterAsync(new(), token);
        var otherCount = await _repo.CountOthersByFilterAsync(new(), token);

        if (personCount > 0 && relationCount > 0 && placeCount > 0 && otherCount > 0)
        {
            return;
        }

        var lines = new List<string>();
        await foreach (var line in _fileService.ReadLinesAsync(filePath, token))
        {
            lines.Add(line);
        };

        var entities = GetEntitiesFromFile(lines.ToArray()).ToList();

        var persons = entities.OfType<PersonRecord>().ToDbo().EnumerateWithIds().ToArray();

        if (personCount == 0)
        {
            await _repo.BulkInsertAsync(persons, token);
        }
        else
        {
            _logger.LogInformation("Step person data already imported... {count} rows", personCount);
        }

        if (relationCount == 0)
        {
            var relations = new List<StepPersonLexiconRelationsDbo>();
            foreach (var person in persons)
            {
                person.Relations.ForEach(x => x.PersonLexiconId = person.Id);
                relations.AddRange(person.Relations);
            }

            await _repo.BulkInsertAsync(relations.EnumerateWithIds().ToArray(), token);
        }
        else
        {
            _logger.LogInformation("Step person relation data already imported... {count} rows", relationCount);
        }

        if (placeCount == 0)
        {
            var places = entities.OfType<PlaceRecord>().ToDbo().EnumerateWithIds().ToArray();
            await _repo.BulkInsertAsync(places, token);
        }
        else
        {
            _logger.LogInformation("Step place data already imported... {count} rows", placeCount);
        }

        if (otherCount == 0)
        {
            var others = entities.OfType<OtherRecord>().ToDbo().EnumerateWithIds().ToArray();
            await _repo.BulkInsertAsync(others, token);
        }
        else
        {
            _logger.LogInformation("Step other data already imported... {count} rows", otherCount);
        }
    }

    private IEnumerable<ILexiconEntry<BibleEntity>> GetEntitiesFromFile(string[] lines)
    {
        var entryLines = new List<(LineType LineType, string Line)>();
        var linetype = LineType.None;
        var recordType = LineType.None;
        var isAnnouncer = false;

        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            linetype = GetLineType(line, linetype);
            isAnnouncer = linetype is LineType.AnnounceOther or LineType.AnnouncePerson or LineType.AnnouncePlace;

            if (isAnnouncer)
            {
                recordType = linetype;
            }

            if ((linetype == LineType.None || isAnnouncer) && IsValidLineSet(entryLines))
            {
                if (recordType is not LineType.None)
                {
                    yield return _factory.GetLexiconStrategy(recordType).Parse(entryLines);
                }

                entryLines = [];
                continue;
            }
            else if (isAnnouncer && !IsValidLineSet(entryLines))
            {
                entryLines = [];
            }

            entryLines.Add((linetype, line));
        }
    }

    private bool IsValidLineSet(List<(LineType LineType, string Line)> entryLines)
    {
        if (entryLines.Count < 5)
        {
            return false;
        }

        if (entryLines.Any(x => x.LineType is LineType.AnnouncePerson or LineType.AnnouncePlace or LineType.AnnounceOther))
        {
            if (!entryLines.Any(x => x.LineType is LineType.None))
            {
                if (entryLines.Any(x => x.LineType is LineType.First))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static LineType GetLineType(string line, LineType oldLineType)
    {
        var parts = line.Split('\t');

        return line switch
        {
            _ when line.StartsWith(PersonSeparator) => LineType.AnnouncePerson,
            _ when line.StartsWith(OtherSeparator) => LineType.AnnounceOther,
            _ when line.StartsWith(PlaceSeparator) => LineType.AnnouncePlace,
            var s when parts.Length == 9 => LineType.First,
            _ when line.StartsWith("– Mentioned") => LineType.Mentioned,
            _ when line.StartsWith("– Total") => LineType.Total,
            _ when line.StartsWith("– Spelled") => LineType.Spelled,
            _ when line.StartsWith("– Named") => LineType.Named,
            _ when line.StartsWith("– Group") => LineType.Group,
            _ when line.StartsWith("– Greek") => LineType.Greek,
            _ when line.StartsWith("– Aramaic") => LineType.Aramaic,
            _ when line.StartsWith("– (same form as previous)") => LineType.SameFormAsPrevious,
            _ when line.StartsWith("@Briefest=")=> LineType.Briefest,
            _ when line.StartsWith("@Brief=")=> LineType.Brief,
            _ when line.StartsWith("@Short=")=> LineType.Short,
            _ when line.StartsWith("@Article=")=> LineType.Article,
            _ => LineType.None,
        };
    }
}
