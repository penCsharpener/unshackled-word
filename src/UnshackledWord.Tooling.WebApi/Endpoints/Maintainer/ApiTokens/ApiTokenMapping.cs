using UnshackledWord.Application.Features.ApiTokens;
using UnshackledWord.Domain.WebApi.Maintainer;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.ApiTokens;

public static class ApiTokenMapping
{
    public static ApiTokenDto ToDto(ApiTokenRecord r, DateTime nowUtc) => new()
    {
        Id = r.Id,
        UserId = r.UserId,
        Name = r.Name,
        Prefix = r.TokenPrefix,
        Scope = r.Scope,
        CreatedByUserId = r.CreatedByUserId,
        CreatedAt = Utc(r.CreatedAt),
        ExpiresAt = Utc(r.ExpiresAt),
        RevokedAt = r.RevokedAt is null ? null : Utc(r.RevokedAt.Value),
        LastUsedAt = r.LastUsedAt is null ? null : Utc(r.LastUsedAt.Value),
        Active = r.RevokedAt is null && Utc(r.ExpiresAt) > nowUtc,
    };

    private static DateTimeOffset Utc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
