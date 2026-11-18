# 0020 · Logic

Every write in `UserAdminService` runs in **one transaction**: lock → guard → write → stamp rotation → `AdminLog` row. Mails are sent **after** commit; a failed mail is logged (Serilog) and reported as a toast, the data change stays.

## 1. Guards

Pure function in `Application/Features/UserAdmin/UserAdminGuards.cs`, unit-tested with a table of cases. Input is a snapshot read under lock.

```csharp
public sealed record GuardSnapshot(int ActorId, int TargetId, bool TargetIsAi, bool TargetIsDeactivated,
    bool TargetIsAdmin, int ActiveAdminCount);           // active = admin role AND DeactivatedAt IS NULL

public static string? CheckSetRoles(GuardSnapshot s, IReadOnlySet<string> newRoles)
{
    if (s.TargetIsAi) return "ai_user";
    var losesAdmin = s.TargetIsAdmin && !newRoles.Contains(AppRoles.Admin);
    if (losesAdmin && s.ActorId == s.TargetId) return "own_admin_role";
    if (losesAdmin && !s.TargetIsDeactivated && s.ActiveAdminCount <= 1) return "last_admin";
    return null;
}

public static string? CheckDeactivate(GuardSnapshot s)
{
    if (s.TargetIsAi) return "ai_user";
    if (s.ActorId == s.TargetId) return "self";
    if (s.TargetIsDeactivated) return "already_deactivated";
    if (s.TargetIsAdmin && s.ActiveAdminCount <= 1) return "last_admin";
    return null;
}
```

`ActorId == TargetId` with `last_admin` is unreachable in practice (actor is an active admin), but both are kept so the guards do not depend on that.

**Lock.** Two admins demoting each other at the same moment must not leave zero admins. Lock all admin role rows plus the target before reading the snapshot:

```sql
SELECT ur."UserId"
FROM "unshackled-word"."UserRoles" ur
WHERE ur."Role" = 'admin'
   OR ur."UserId" = @targetId
ORDER BY ur."UserId"
FOR UPDATE;

SELECT u."Id",
       u."DeactivatedAt" IS NOT NULL                                              AS "IsDeactivated",
       EXISTS (SELECT 1 FROM "unshackled-word"."UserRoles" r WHERE r."UserId" = u."Id" AND r."Role" = 'ai')    AS "IsAi",
       EXISTS (SELECT 1 FROM "unshackled-word"."UserRoles" r WHERE r."UserId" = u."Id" AND r."Role" = 'admin') AS "IsAdmin",
       (SELECT count(*)
        FROM "unshackled-word"."UserRoles" r
        JOIN "unshackled-word"."Users" a ON a."Id" = r."UserId"
        WHERE r."Role" = 'admin'
          AND a."DeactivatedAt" IS NULL)                                          AS "ActiveAdminCount"
FROM "unshackled-word"."Users" u
WHERE u."Id" = @targetId
FOR UPDATE OF u;
```

`ORDER BY` keeps lock order stable so two such transactions queue instead of deadlocking. Deactivate/reactivate use the same snapshot query.

## 2. Set roles

```
requested  = distinct(request.Roles) ∩ {admin, maintainer, reviewer}
current    = roles of target ∖ {user, ai}
toGrant    = requested ∖ current
toRevoke   = current ∖ requested
if toGrant ∪ toRevoke is empty → return detail, no log, no stamp rotation
guard CheckSetRoles(snapshot, requested)
DELETE … WHERE "UserId" = @id AND "Role" = ANY(@toRevoke)
INSERT ("UserId","Role","GrantedByUserId") SELECT @id, unnest(@toGrant), @actorId ON CONFLICT DO NOTHING
INSERT "user" ON CONFLICT DO NOTHING                      -- repairs accounts that lack the base role
rotate stamp; one AdminLog row per granted and per revoked role
```

## 3. Deactivate / reactivate

```sql
-- deactivate (after CheckDeactivate)
UPDATE "unshackled-word"."Users"
SET "DeactivatedAt"       = now(),
    "DeactivatedByUserId" = @actorId,
    "DeactivationReason"  = NULLIF(btrim(@reason), ''),
    "SecurityStamp"       = gen_random_uuid()::varchar,
    "UpdatedAt"           = now()
WHERE "Id" = @targetId
  AND "DeactivatedAt" IS NULL;

-- open emailed tokens die with the account
UPDATE "unshackled-word"."AuthTokens"
SET "ConsumedAt" = now()
WHERE "UserId" = @targetId
  AND "ConsumedAt" IS NULL;
```

Reactivate reads the old values for the log row (`Details`), then sets the three columns to NULL. No stamp rotation needed (there are no sessions). Passkeys and password stay; the person signs in as before.

## 4. Sign-in enforcement

One source of truth: `AuthUser` and `PasskeyLoginUser` get `bool IsDeactivated` (`u."DeactivatedAt" IS NOT NULL` in their SELECTs).

| Path | Behaviour for a deactivated account |
|---|---|
| Cookie (`OnValidatePrincipal`) | `GetSecurityStampAsync` adds `AND "DeactivatedAt" IS NULL` → null → principal rejected. Stamp rotation already ended it; this is the second fence. |
| `Login` | verify password first; wrong → generic 401 as today; right → 403 `account_deactivated` (no enumeration of deactivated accounts without the password) |
| `PasskeyLogin` | after signature check → 403 `account_deactivated` |
| `MagicLinkLogin` | `ConsumeMagicLinkAsync` joins `Users` and requires `DeactivatedAt IS NULL` → generic invalid-link error (tokens were consumed at deactivation anyway) |
| `ResetPassword`, `VerifyEmail` | same join → generic invalid-link error |
| `RequestMagicLink`, `RequestPasswordReset` | lookup finds the user, `IsDeactivated` → return the normal 200, send nothing |
| `Register` | email exists → existing "email taken" behaviour, unchanged |

`SessionSignIn.SignInAsync` gets `IAuthRepository` and runs `UPDATE "Users" SET "LastSignInAt" = now() WHERE "Id" = @userId` after a successful sign-in, so every path sets it in one place.

## 5. Create user

```
normalized = email.Trim().ToUpperInvariant()
tx:
  INSERT Users (Email, EmailNormalized, PasswordHash = NULL, DisplayName, EmailVerifiedAt = now())
    ON CONFLICT ("EmailNormalized") DO NOTHING RETURNING "Id"
  no id → SELECT existing id → 409 email_taken { existingUserId }
  INSERT UserRoles: 'user' (GrantedByUserId NULL) + each requested role (GrantedByUserId = actor)
  AdminLog user_created
  token = IssueToken(id, password_reset, 72 h, supersedeOpen: true)
  AdminLog welcome_sent { expiresAt }
commit → send welcome mail (template "Willkommen bei Unshackled Word – Passwort festlegen",
         link {FrontendBaseUrl}/reset-password?token=…&welcome=1)
```

`IssueTokenAsync` gets `supersedeOpen`: before inserting, `UPDATE AuthTokens SET ConsumedAt = now() WHERE UserId = @userId AND Purpose = @purpose AND ConsumedAt IS NULL`. Used by welcome, admin reset and the self-service reset, so only the newest link works.

**Admin "Passwort-Reset-Mail senden"**: `HasPassword` false → welcome mail (72 h, `welcome_sent`); true → normal reset mail (1 h, `password_reset_sent`). Deactivated → 409 `deactivated`.

## 6. Edit details

```
tx: lock target row
  displayName changed → set
  email changed (by normalized compare):
    another user has it → 409 email_taken
    set Email, EmailNormalized, EmailVerifiedAt = now(), rotate stamp
  nothing changed → return, no log
  AdminLog user_updated with only the changed keys
commit → email changed: notice to old address ("Deine E-Mail-Adresse wurde von einem Administrator geändert auf …")
         and to new address ("Dein Konto verwendet jetzt diese Adresse")
```

A display-name-only change does **not** rotate the stamp; the header shows the old name until the next sign-in (the claim is in the cookie). Accepted.

## 7. List query

```sql
SELECT u."Id", u."Email", u."DisplayName", u."CreatedAt", u."LastSignInAt",
       array_agg(r."Role" ORDER BY r."Role") FILTER (WHERE r."Role" IS NOT NULL) AS "Roles",
       CASE
           WHEN bool_or(r."Role" = 'ai')       THEN 'ai'
           WHEN u."DeactivatedAt" IS NOT NULL THEN 'deactivated'
           ELSE 'active'
       END                                                                      AS "Status",
       count(*) OVER ()                                                         AS "Total"
FROM "unshackled-word"."Users" u
LEFT JOIN "unshackled-word"."UserRoles" r ON r."UserId" = u."Id"
WHERE (@search IS NULL OR u."DisplayName" ILIKE @pattern OR u."Email" ILIKE @pattern)
GROUP BY u."Id"
HAVING (@role IS NULL OR bool_or(r."Role" = @role))
   AND (@status IS NULL OR <Status expression> = @status)
ORDER BY lower(u."DisplayName"), u."Id"
LIMIT @pageSize OFFSET (@page - 1) * @pageSize;
```

`@pattern` = `%` + search with `\`, `%`, `_` escaped + `%`. The status `CASE` is repeated in `HAVING` (Postgres cannot reference the alias there).

## 8. Frontend rules (mirror of the guards, for disabling only)

- Gear and admin routes: `user.roles.includes("admin")`; `RequireRole role="admin"` → anonymous: redirect to `/login?returnTo=…`, signed in without role: `admin.forbidden` note.
- Role checkbox *Admin* disabled when `detail.isSelf` or (`detail.isLastActiveAdmin` and checked); "Deaktivieren" hidden when `isSelf`, disabled with hint when `isLastActiveAdmin`.
- Status `ai` → whole page read-only.
- After saving roles/email/status of **oneself** (only display name or granting oneself non-admin roles are possible): the stamp rotated, so the next request returns 401 and `AuthContext` sends the admin to sign-in with a toast "Bitte erneut anmelden".
- 409 `code` → inline error at the section that caused it; list and detail refetch after every write.
