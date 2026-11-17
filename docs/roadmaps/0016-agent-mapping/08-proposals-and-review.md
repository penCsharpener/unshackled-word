# 0016 · Proposals and review

## Tables (new migration script)

| Table | Columns (main) |
|---|---|
| `AgentRuns` | Id, CreatedByUserId (maintainer or agent user), AiUserId, Profile, Model, PromptVersion, Range (book, from ref, to ref), Mode (`fill-open` / `redo`), Status, token totals, CreatedAt, FinishedAt |
| `AgentPackages` | Id (public `packageId`), RunId, verse refs, WordIdsHash, LocalIdMap (jsonb), Status, token counts, attempts, ExpiresAt |
| `AgentVerseProposals` | Id, RunId, PackageId, BookId, Chapter, Verse, Language, Links (jsonb, real ids), Lemmas (jsonb), Parts (jsonb), Flags (jsonb), Note, Status (`open` / `merged` / `edited-merged` / `discarded` / `superseded`), ReviewedByUserId, ReviewedAt |

A new proposal for the same verse supersedes the open older one. Proposals are not part of the seed; merged results reach the seed through the normal mapping/lemma export.

Users: one row per model in `Users` with role `Ai` (no password, no login); roles are plain strings in `UserRoles`, `AppRoles.Ai` exists since 0016.2. MCP tokens: `ApiTokens` (UserId, hash, scope `propose`, CreatedByUserId, ExpiresAt, RevokedAt, LastUsedAt); only the hash is stored, the token is shown once at creation.

## Merge

Merging a verse writes through the existing paths, so live data has one writer:

- Links → `VerseMappingService.SaveAsync` (needs an author parameter; see task 0016.2).
- Lemmas → `ILemmaRepository.ApplyAsync(changes, userId)`, audited in `Elb1871MorphologyEdits` with the AI user as author and the maintainer as reviewer.

Merge re-runs the hard rules against the live verse; a verse that changed since the proposal is not merged and shows "veraltet".

## UI (tagging area, maintainer only)

- **Range action** on the chapter view: "KI neu zuordnen …" → dialog with range, profile, mode, estimated tokens/cost → starts a run. Progress uses the existing maintainer job status pattern.
- **Proposal list** ("Wartung" > "KI-Vorschläge"): per run, verses with status and flag count, filter "nur markierte".
- **Verse review**: the existing `MappingEditor` in a compare mode: current links vs. proposal (added/removed links coloured with tokens), lemma changes as a small table, flags and the model note above. Buttons: Übernehmen, Bearbeiten (opens the editor with the proposal loaded, save = edited-merged), Verwerfen. Keyboard: next/previous verse.
- **Bulk**: "alle ohne Markierung übernehmen" for a run, with confirmation and count.

New components (compare mode as a variant of `MappingEditor` if possible) are proposed in chat and registered in `Components.md` before building.
