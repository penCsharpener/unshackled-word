using UnshackledWord.Domain.Models.Dbo.Revision;
using UnshackledWord.Domain.WebApi.Revision;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Revision;

public enum SaveRevisionLevelOutcome
{
    Saved,
    NotFound,
    Invalid,
    Conflict,
}

public sealed class SaveRevisionLevelResult
{
    public SaveRevisionLevelOutcome Outcome { get; init; }
    public string? Error { get; init; }
    public RevisionLevelDetail? Level { get; init; }
}

/// <summary>Preset management of the revision slider (roadmap 0011.7); shape rules live in the endpoint validator, database rules here.</summary>
public sealed class RevisionLevelService
{
    private readonly IRevisionLevelRepository _levels;

    public RevisionLevelService(IRevisionLevelRepository levels)
    {
        _levels = levels;
    }

    public async Task<List<RevisionLevelDetail>> GetDetailsAsync()
    {
        var all = await _levels.GetAllAsync();
        var counts = await _levels.GetVariantCountsAsync();
        return all.OrderBy(l => l.Level).Select(l => ToDetail(l, counts.GetValueOrDefault(l.Level))).ToList();
    }

    public async Task<SaveRevisionLevelResult> CreateAsync(SaveRevisionLevelRequest request)
    {
        var all = await _levels.GetAllAsync();

        if (all.Any(l => !l.IsBase && l.Level == request.Level))
        {
            return Failed(SaveRevisionLevelOutcome.Conflict, "A preset with this level already exists.");
        }

        if (all.Any(l => l.Code == request.Code))
        {
            return Failed(SaveRevisionLevelOutcome.Conflict, "A preset with this code already exists.");
        }

        if (request.IsDefault && all.Any(l => l.IsDefault))
        {
            return Failed(SaveRevisionLevelOutcome.Invalid, "There is already a default preset; set the new default on that preset instead.");
        }

        var level = ToDbo(request);
        await _levels.InsertAsync(level);
        return new SaveRevisionLevelResult { Outcome = SaveRevisionLevelOutcome.Saved, Level = ToDetail(level, 0) };
    }

    public async Task<SaveRevisionLevelResult> UpdateAsync(SaveRevisionLevelRequest request)
    {
        var all = await _levels.GetAllAsync();
        var current = all.FirstOrDefault(l => !l.IsBase && l.Level == request.CurrentLevel);

        if (current is null)
        {
            return Failed(SaveRevisionLevelOutcome.NotFound, null);
        }

        var counts = await _levels.GetVariantCountsAsync();
        var used = counts.GetValueOrDefault(current.Level);

        if (request.Level != current.Level)
        {
            // Variants refer to the revision by id, so the level can move freely; it only has to stay unique among the revisions with variants.
            if (all.Any(l => !l.IsBase && l.Level == request.Level))
            {
                return Failed(SaveRevisionLevelOutcome.Conflict, "A preset with this level already exists.");
            }
        }

        if (all.Any(l => l.Id != current.Id && l.Code == request.Code))
        {
            return Failed(SaveRevisionLevelOutcome.Conflict, "A preset with this code already exists.");
        }

        if (current.IsDefault && !request.IsDefault)
        {
            return Failed(SaveRevisionLevelOutcome.Invalid, "Exactly one preset must be the default; set another preset as default instead.");
        }

        var level = ToDbo(request);
        await _levels.UpdateAsync(current.Level, level);
        return new SaveRevisionLevelResult { Outcome = SaveRevisionLevelOutcome.Saved, Level = ToDetail(level, used) };
    }

    private static SaveRevisionLevelResult Failed(SaveRevisionLevelOutcome outcome, string? error) => new() { Outcome = outcome, Error = error };

    private static RevisionLevelDbo ToDbo(SaveRevisionLevelRequest r) => new()
    {
        Level = r.Level,
        Code = r.Code.Trim(),
        NameDe = r.NameDe.Trim(),
        NameEn = r.NameEn.Trim(),
        DescriptionDe = string.IsNullOrWhiteSpace(r.DescriptionDe) ? null : r.DescriptionDe.Trim(),
        DescriptionEn = string.IsNullOrWhiteSpace(r.DescriptionEn) ? null : r.DescriptionEn.Trim(),
        IsBase = false,
        IsDefault = r.IsDefault,
    };

    private static RevisionLevelDetail ToDetail(RevisionLevelDbo l, int variantCount) => new()
    {
        Level = l.Level,
        Code = l.Code,
        NameDe = l.NameDe,
        NameEn = l.NameEn,
        DescriptionDe = l.DescriptionDe,
        DescriptionEn = l.DescriptionEn,
        IsBase = l.IsBase,
        IsDefault = l.IsDefault,
        VariantCount = variantCount,
    };
}
