# 0020 · API

Endpoints under `src/UnshackledWord.Tooling.WebApi/Endpoints/Admin/Users/<Name>/Endpoint.cs` and `Endpoints/Admin/Log/Endpoint.cs`, group `admin` (route prefix `api/admin`), every one `Roles(AppRoles.Admin)`. Contracts in `Domain.WebApi/UserAdmin/`. Logic in `Application/Features/UserAdmin/` (`UserAdminService`, `UserAdminGuards`, `AdminActions`), SQL in `Infrastructure/Repositories/UserAdminRepository.cs` + `UserAdminSql.cs`, `AdminLogRepository.cs`. Behaviour: [06-logic.md](06-logic.md).

## Endpoints

| Method + route | Request | Response | Errors |
|---|---|---|---|
| `GET users` | query `search?`, `role?`, `status?` (`active`/`deactivated`/`ai`), `page` (1), `pageSize` (50, max 200) | `UserListResponse` | 400 bad filter |
| `GET users/{id}` | – | `UserDetail` | 404 |
| `POST users` | `CreateUserRequest` | 201 `UserDetail` | 409 `email_taken` (`existingUserId`) |
| `PUT users/{id}` | `UpdateUserRequest` | `UserDetail` | 404, 409 `email_taken`, 409 `ai_user` |
| `PUT users/{id}/roles` | `SetRolesRequest` | `UserDetail` | 404, 409 `ai_user` / `own_admin_role` / `last_admin` |
| `POST users/{id}/deactivate` | `DeactivateRequest` | `UserDetail` | 404, 409 `ai_user` / `self` / `last_admin` / `already_deactivated` |
| `POST users/{id}/reactivate` | – | `UserDetail` | 404, 409 `not_deactivated` |
| `POST users/{id}/password-reset` | – | 204 | 404, 409 `ai_user` / `deactivated` |
| `GET log` | query `userId?`, `action?`, `page`, `pageSize` (50) | `AdminLogResponse` | 400 unknown action |

409s are problem details with a `code` from the list above; the frontend maps `code` to an i18n key (`admin.users.errors.<code>`).

## Contracts

```csharp
public sealed record UserListItem(
    int Id, string Email, string DisplayName, string[] Roles, string Status,   // "active" | "deactivated" | "ai"
    DateTimeOffset CreatedAt, DateTimeOffset? LastSignInAt);

public sealed record UserListResponse(UserListItem[] Items, int Total, int Page, int PageSize);

public sealed record UserDetail(
    int Id, string Email, string DisplayName, string[] Roles, string Status,
    DateTimeOffset CreatedAt, DateTimeOffset? LastSignInAt, bool EmailVerified, bool HasPassword, int PasskeyCount,
    DeactivationInfo? Deactivation,
    bool IsSelf, bool IsLastActiveAdmin);                                    // drive disabled controls in the UI

public sealed record DeactivationInfo(DateTimeOffset At, int ByUserId, string ByDisplayName, string? Reason);

public sealed record CreateUserRequest(string Email, string DisplayName, string[] Roles);
public sealed record UpdateUserRequest(string DisplayName, string Email);
public sealed record SetRolesRequest(string[] Roles);                       // assignable roles only; "user" implicit
public sealed record DeactivateRequest(string? Reason);

public sealed record AdminLogItem(
    long Id, DateTimeOffset CreatedAt, string Action,
    int ActorUserId, string ActorDisplayName,
    int? TargetUserId, string? TargetDisplayName, int? InviteCodeId,
    JsonElement Details);

public sealed record AdminLogResponse(AdminLogItem[] Items, int Total, int Page, int PageSize);
```

## Validation (FluentValidation)

| Field | Rule |
|---|---|
| `Email` | not empty, ≤ 254, `EmailAddress()`; trimmed; normalized = `ToUpperInvariant()` as in `Register` |
| `DisplayName` | trimmed, 1–100 |
| `Roles` | each in `AssignableRoles = [admin, maintainer, reviewer]`, no duplicates; empty allowed (plain user) |
| `Reason` | ≤ 500 |
| `search` | ≤ 100; `role` in `AppRoles.All`; `status` in the three values; `page` ≥ 1; `pageSize` 1–200 |
| `action` | in `AdminActions.All` |

## Changed existing endpoints

| Endpoint | Change |
|---|---|
| `Auth/Login`, `MagicLinkLogin`, `PasskeyLogin`, `ResetPassword`, `VerifyEmail` | refuse deactivated accounts ([06-logic.md](06-logic.md#sign-in-enforcement)); set `LastSignInAt` |
| `Auth/RequestMagicLink`, `RequestPasswordReset` | send nothing for deactivated accounts, same 200 |
| `Admin/CreateInviteCode`, `RevokeInviteCode`, `InviteByEmail` | write `AdminLog` row in the same transaction |
| `Auth/Me` | unchanged (roles still from claims; stale claims are impossible because changes rotate the stamp) |

Bruno: one request per endpoint in `api/unshackled-word/Admin/Users/`.
