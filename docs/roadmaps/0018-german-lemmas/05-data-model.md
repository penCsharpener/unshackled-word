# 0018 · Data model, backfill, stats

## Migration `0030_LinkRoles.sql`

| Table | Column | Type | Default / note |
|---|---|---|---|
| `Elb1871GreekMapping` | `Role` | `varchar(4)` NOT NULL, CHECK in (`head`, `unit`) | `head` |
| `Elb1871HebrewMapping` | `Role` | same | `head` |
| `Elb1871HebrewMapping` | `StrongsNumber` | `varchar(10)` NULL | morpheme rendered; NULL = main (braced) morpheme until backfilled |
| `RevisionMapping` | `Role`, `StrongsNumber` | same | `head` / NULL |
| all three | `PartLemma` | `varchar(40)` NULL | only on rows with `GermanWordPart` |
| `AgentVerseProposals` | `Roles` | `jsonb` NULL | role/morpheme diffs, like `Lemmas` |

Added-word rows keep `Role = head` but are ignored (they are not real links). Index `("StrongsNumber")` on the Hebrew table, like the Greek one.

Built in 0018.1 with one extra column: `MultiHead` `boolean` NOT NULL DEFAULT `false` on all three tables, the stored "multi-head accepted" flag of invariant 1 (needed by check `R2` and so a reloaded verse keeps it). It is true on every `head` row of a morpheme whose several heads were accepted, false elsewhere. CHECKs: `Role` in (`head`, `unit`); `PartLemma` only with `GermanWordPart`; on `RevisionMapping` `StrongsNumber` only for `Lang = 'H'`. Hebrew `StrongsNumber` holds the morpheme tag as `DisambiguatedStrongs` spells it, braces stripped (`H9001`, `H0430G`). The revision carry-over does not transport roles yet: carried rows start as `head` on the main morpheme.

## Save contract (0018.2)

`VerseMappingLink` (GET response and PUT body, also the revision mapping) gains `role` (`head`/`unit`, always set in responses, null = head in a save), `strongsNumber` (Hebrew morpheme, null = main; Greek links omit it, an echoed own Strong's is accepted), `partLemma` (only with `germanWordPart`, max 40) and `allowMultiHead` (`true` on one head link accepts several heads on that morpheme; stored as `MultiHead` on all its heads and returned `true` on each). Hebrew `originalWords[]` gain `morphemes` (`strongs`, `isMain`, `original`, `gloss`) parsed from `ExpandedStrongTags`, punctuation H9014–H9018 left out.

- **Role-aware save**: at least one link sets `role`, `strongsNumber`, `partLemma` or `allowMultiHead`. All invariants are checked, including one head per morpheme. A client that knows roles sends `role` on every real link.
- **Legacy save** (no role field anywhere: the current frontend, merged 0016 agent proposals): each link keeps role, morpheme, `MultiHead` and part lemma of the stored row with the same (`elbWordId`, `stepWordId`); new links default to `head`/NULL; a kept `unit` whose morpheme lost its head becomes `head`. The one-head rule is skipped, because all links are `head` until the backfill (0018.4).
- Validator (`SaveVerseMappingValidator`): unknown role; role `unit`, `strongsNumber` or `allowMultiHead` on an added word; `allowMultiHead` on a unit; Hebrew `strongsNumber` not a morpheme of the linked word or punctuation; `partLemma` without part; `unit` without a head on the same morpheme, except a German `APPRART` word that is the head of another morpheme and a unit of the article (G3588 / H9009); more than one head without `allowMultiHead` (role-aware only). Invariant 3 holds through the existing unique (`elbWordId`, `stepWordId`) check. Invariant 4 needs lemmas and is left to check `R4` (0018.6). Revision words have no STTS tag, so the `APPRART` exception does not apply to revision mappings.

### Frontend save rule (0018.2f)

`MappingEditor` sends the role fields only when it must (`needsRoleFields` in `lib/bible/mappingEditor.ts`): the loaded verse already has role data (a `unit`, a `strongsNumber`, a `partLemma` or `allowMultiHead`), or this edit session set some. Otherwise it sends the legacy payload (no role field at all), so an unrelated edit of a verse with `habe getauft` both head is not rejected by the one-head rule before the backfill (0018.4). A role-aware save carries `role` on every real link, `strongsNumber` only for a picked non-main morpheme (or a main one the server returned explicitly), `allowMultiHead` on every head of an accepted morpheme, `partLemma` where set. The editor mirrors invariants 1 and 2 as hints (not blocking; the `APPRART` exception is the server's) and offers "Mehrere Kerne zulassen" on a morpheme with several heads; the server's 400 messages are listed with word ids replaced by the words.

## Deterministic backfill (job `link-roles`)

Maintainer page "Wartung" > "Link-Rollen", background job like the seed jobs. Dry run first: a report with counts per rule and 20 sample verses each; then apply as one change set (`Elb1871MorphologyChangeSets` gains a `Kind`, or a sibling `MappingChangeSets` table: decided in 0018.3). Undo restores the before values.

Rules, in order, applied only when exactly one rule fits:

1. **Hebrew morpheme by tag.** German word linked to a Hebrew word with prefix/suffix morphemes: `KON und` → H9001/H9002, `ART` → H9009, `APPR in/an/mit` → H9003, `zu/nach/für` → H9005, `wie` → H9004, `von/aus` → H9006, possessive/personal pronoun by person, number and gender → the matching suffix morpheme (H9020–H9038); everything else → main morpheme. A table `HebrewMorphemeHints` in Application, unit-tested.
2. **Auxiliary unit.** `VA*`/`VM*` linked to the same morpheme as a `VV*`/participle → `unit`.
3. **Particle unit.** `PTKVZ` linked to the same morpheme as a finite verb whose lemma starts with the particle → `unit`.
4. **Reflexive / `zu` unit.** `PRF` or `PTKZU` linked to the same morpheme as a verb → `unit`.
5. **Subject pronoun → added word.** `PPER` (nominative) linked to a finite-verb morpheme that has no pronoun morpheme and a matching person → `IsAddedWord = true`, `ParentGermanWordId` = the verb's German word.

6. **Particle lemma reset.** A `PTKVZ` token whose lemma is not its own text (0016 convention, `aus` → `ausgehen`) gets its text as lemma, via `Elb1871MorphologyChangeSets`; the finite verb keeps the full lemma.

Everything else stays `head` and is listed by the new data-quality check (below) as input for the model.

### As built (0018.3, `Application/Features/LinkRoles`)

`LinkRoleRules.Decide(LinkRoleVerse)` evaluates every rule against the input state. One fitting rule (or several with the same outcome) decides; different outcomes leave the link undecided with the fitting rules and a reason. Rule 1 has two report ids: `1` (hint matched) and `1-main` (fallback). Deviations found in the data (uw_0018_test):

- Rule 1 hints: any `KON` (not only `und`: `aber` renders ו too); `ART` also H9010 (Aramaic article); `bei` → H9003; `nach` also H9011 (directional ה); prepositions also under `APPRART`/`APPO`/`APZR`/`PTKVZ` (Elb1871 tags many prepositions `PTKVZ`, Mk 1:29 `aus`); `wie` under `KOKOM`/`KOUS`/`PWAV`/`APPR`. Pronoun suffixes are H9020–H9049 (H9040–H9049 are subject suffixes, Rt 3:4 `er` on בְּשָׁכְבוֹ); `PRF` is matched too; person/number/gender come from the pronoun form, not from Elb1871Morphology (a possessive's number/gender there is its noun's).
- Rule 4: Elb1871 has no `PTKZU`; infinitive `zu` is `PTKVZ` (10,922 rows). `zu` tagged `PTKVZ` right before a `*INF` counts as `PTKZU`. On a Hebrew infinitive with ל it also fits rule 1 (H9005), so it stays undecided.
- Rule 5: participles count as 3rd person (Mk 1:29 ἐξελθόντες, Mk 1:35 ἀναστὰς); the parent is the nearest German finite verb on the morpheme (Rt 3:4 `du … tun sollst` → `sollst`).
- Two rules with different outcomes, real case: Rt 3:4 `wenn er sich niederlegt`, `sich` → rule 1 (suffix H9043) vs rule 4 (unit of H7901G).
- Morpheme parsing also drops H9019 (section mark, 2 rows) and the instance suffix `#n` (`H9033#6`, 1 row); `VerseMapping/HebrewMorphemes` (0018.2) kept both until 0018.4 made it delegate here.
- Rule 6 finds nothing in uw_0018_test (no `PTKVZ` lemma differs from its text); it matters for data with the 0016 convention.
- The change-set question (`Kind` vs `MappingChangeSets`) is left to 0018.4.

### Rule 3b (0018.4)

**3b. Separable verb, lemma not joined yet.** `PTKVZ` linked to the same morpheme as exactly one finite verb whose lemma does not start with the particle → particle `unit`, and the verb's Elb1871Morphology lemma becomes particle + lemma (`stand … auf`: `stehen` → `aufstehen`), but only when that combination already is a lemma somewhere in Elb1871Morphology (the job builds the set once per run and passes it to `LinkRoleRules.Decide(verse, knownLemmas)`; null switches 3b off). Unknown combination → undecided "separable verb, unknown combination" (for the model, even if rule 1 would fit); several finite verbs on the morpheme or two particles wanting different lemmas on one verb → undecided too. Once joined, a second run finds rule 3 instead (idempotent). The verb lemma change goes through `Elb1871MorphologyEdits` of the change set like rule 6. Report id `3b`.

### Change set and job (as built, 0018.4)

- **Storage decision:** the header is an `Elb1871MorphologyChangeSets` row (the table already has `Kind`): `Kind = 'linkRoles'` for an apply, `'linkRolesUndo'` with `RevertOf` for its undo; `Scope` holds `mappingRows` and `previousLinkRolesAppliedAt`, `WordCount` the lemma rows. Lemma changes (rules 3b, 6) are ordinary `Elb1871MorphologyEdits` rows with that `ChangeSetId` (full before/after values like the bulk lemma edit), so the existing lemma audit and `RefreshVerseLemmaText` apply. Mapping changes go to the new table `MappingChangeSetRows` (`0033_LinkRoleChangeSets.sql`): one row per changed mapping row with typed `Before*`/`After*` columns for `Role`, `StrongsNumber`, `IsAddedWord`, `ParentGermanWordId`, `StepWordId` (NULLs kept). A sibling header table was not needed; the lemma page's revert refuses `Kind <> 'bulkLemma'`, so a backfill can only be undone as a whole by the job.
- **Apply** recomputes the plan, then in one transaction writes header, change rows (batches of 10,000 via `unnest`), updates both mapping tables where every before value still holds (count mismatch → exception, rollback, "run again"), writes the lemma edits the same way, refreshes `LemmaText` of the touched verses and sets `DqRefreshState."LinkRolesAppliedAt" = now()` (only when the column exists, added by 0032). **Undo** (latest not-undone `linkRoles` set) restores the before values where the row still holds the after values (rows edited since are skipped and counted), restores lemmas the same way, puts `LinkRolesAppliedAt` back to the stored previous value and sets `RevertedAt`.
- **Rule 5 rows** become `IsAddedWord = true`, `ParentGermanWordId` = verb, `StepWordId` and `StrongsNumber` NULL (the editor's save writes added rows without `StepWordId`; 49,850 of 59,024 imported Hebrew added rows still carry one, Greek: 624 of 32,986), role head. A pronoun that has another row in the same table stays undecided ("rule 5: subject pronoun with several rows"), because the unique (`ElbWordId`, `StepWordId`) key allows only one added row per word.
- Only rows whose values change are written; a second apply on backfilled data writes nothing. The German text the rules see is `PlainWord` (no punctuation, `auf,` → `auf`), so rule 6 never writes punctuation.
- **Report:** `Decided`/`Changed` per language (`greek`, `hebrew`, `german` for the lemma rules) and rule id, undecided count and per language by reason group (ids and morpheme lists stripped), 20 sample verses per rule (spread by a hash of `HebRefId`, stable between runs) with before/after per changed link (`head (main)` → `head H0559`, `head G0450` → `unit G0450`, `added → stand`, `lemma stehen` → `lemma aufstehen`).
- **Job:** `POST maintainer/link-roles/jobs` (`mode` = `dryRun` | `apply` | `undo`), `GET maintainer/link-roles/status` (job with log and report, the change set undo would restore, `LinkRolesAppliedAt`); Admin/Maintainer. `LinkRoleJobRunner` takes the seed lock (`SeedExportCoordinator`), so it never runs beside a seed job or export; it refuses (409) while an agent run is `queued`/`running`/`waiting-for-provider` (checked at start and again before writing), and agent runs refuse to start while it runs.
- **Hebrew morpheme parsing** is one implementation: `VerseMapping/HebrewMorphemes.Parse` delegates to `LinkRoles/SourceMorphemes.ParseHebrew`. Both now drop H9014–H9019, strip `#n`, merge a repeated Strong's (main if any piece is braced) and fall back to the first non-prefix morpheme as main when nothing is braced; so a morpheme the backfill writes is always one the save validator accepts.

## Stats

- `DqWordFacts` gains `Role`, `MorphemeStrongs` (Greek: `StrongsNumber`; Hebrew: link column, else main morpheme) and `RenderLemma` (`PartLemma` or lemma).
- `StatsStrongsRenderings` groups by morpheme Strong's and **rendering key** (head lemmas of the instance, [04-roles-and-counting.md](04-roles-and-counting.md)), counts distinct instances, reads only `head` rows. One migration drops and recreates the chain, as 0014 did.
- New data-quality checks: `R1` morpheme with links but no head, `R2` more than one head without the accepted flag, `R3` `unit` without head, `R4` same lemma twice on one morpheme, `R5` `PTKVZ` without a matching verb, `R6` legacy spelling as lemma.

### As built (0018.6, `0032_LinkRoleStats.sql`)

- SQL function `"HebrewMorphemes"(tags)` is the only SQL parser of `ExpandedStrongTags` (first `=` field, braces, trailing `+` and `#n` stripped, H9014–H9019 dropped, duplicates keep the first, braced piece = main; no brace: first non-prefix, else first). `DqWordFacts` uses it for the main morpheme, `StatsStrongs` for all morphemes.
- `DqWordFacts` also gains `PartLemma`, `RenderLemmaLower`, `MultiHead` (aligned rows) and `Stts` (all German rows, snapshot of `Elb1871Morphology`, used by R1/R3/R5). Greek `MorphemeStrongs` = link `StrongsNumber`, else the word's `DisambiguatedStrongs` (identical on all 134,595 filled rows). Hebrew link values are read with `#n` stripped.
- `StatsStrongsRenderings`: `StrongsKey` = morpheme Strong's; `GermanKey` = head render lemmas (form when no lemma) in verse order, space-joined; `Pos` = the head's PoS, or the PoS list space-joined when there are several heads (`AUX VERB`); `SourceCount` = distinct instances. Instances with links but no head (all `unit`) form one row with `GermanKey = ''`, `GermanDisplay = ''` and new column `NoOwnLemma = true` — the contraction article ("rendered, no own lemma"); a headless instance that is not a contraction lands there too (so the sum stays exact) and is listed by R1. The API returns it as a rendering with empty `word`/`key` and `noOwnLemma: true` (`StrongsRenderingResponse.NoOwnLemma`).
- `StatsStrongs` counts source morpheme instances (Greek: one per word; Hebrew: one per morpheme of `ExpandedStrongTags`, plus any morpheme a link names); `MappedCount` = instances with a real link on that morpheme. Prefix/suffix Strong's (H9001 …) now have rows (H9001: 21,328 occurrences, 0 mapped before the backfill). Main-morpheme numbers are unchanged while all links sit on the main morpheme.
- Known limits: the reverse lookups (word stats "originals of lemma X") join `GermanKey` to a single lemma, so multi-head keys and part lemmas of compounds (`gerste` of `Gerstenernte`) are not found under the whole-word lemma; the concordance filter by rendering key still filters by single lemma.
- Checks (`Endpoints/DataQuality/DqRoleChecks.cs`, listed after L7): R1 one row per headless instance (error), R2 head rows of instances with more than one head and not all `MultiHead` (warning), R3 `unit` rows without a head on the morpheme (error), R4 links whose render lemma appears twice on one morpheme (warning), R5 `PTKVZ` words without a finite verb in the verse whose lemma starts with the particle and is longer (`CheckParticle`), infinitive `zu` (directly before `*INF`/`*IZU`) skipped (warning), R6 lemma or part lemma in `AgentLemmaRules.LegacySpellings`, passed as parameter `@legacy` (error). R1 and R3 except the contraction: an `APPRART` unit on G3588/H9009 whose word is a head elsewhere.
- `DqRefreshState.LinkRolesAppliedAt` (timestamptz NULL), set by the backfill; returned as `linkRolesAppliedAt` by `GET` Strong's summary (snapshot path) and the data-quality overview. NULL = "Rollen unvollständig".
- Done-when query (returns `0|0` on uw_0018_test after the refresh):

```sql
SELECT count(*) AS "Mismatches", count(*) FILTER (WHERE s."Lang" IS NULL OR r."Lang" IS NULL) AS "OneSided"
FROM (SELECT x."Lang", x."StrongsKey", sum(x."SourceCount") AS "Sum" FROM "unshackled-word"."StatsStrongsRenderings" x GROUP BY 1, 2) r
    FULL JOIN (SELECT y."Lang", y."StrongsKey", y."MappedCount" FROM "unshackled-word"."StatsStrongs" y WHERE y."MappedCount" > 0) s
        ON s."Lang" = r."Lang" AND s."StrongsKey" = r."StrongsKey"
WHERE r."Sum" IS DISTINCT FROM s."MappedCount";
```

## Seed

The mapping seed (`assets/seed`, `MappingSeedImporter`/`MappingExportRepository`) carries `role`, `strongs` (Hebrew), `partLemma` and `multiHead`; old files import with defaults (`head`, NULL, NULL). The revision seed carries the same on `RevisionMapping`. Format version bumped once (mapping seed 2 -> 3, detected by the `Role` header; the revision seed has no format field and is detected the same way). Defaults are written as empty cells.
