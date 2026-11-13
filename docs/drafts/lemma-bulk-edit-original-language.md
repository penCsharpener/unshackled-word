# Lemma bulk edit – guard with the original-language mapping (evaluation)

Status: evaluation only, nothing implemented. Builds on `docs/drafts/lemma-bulk-edit-design.md` and the code that already exists (`Endpoints/Lemmas/LemmaBulkRepository.cs`, `Domain.WebApi/Lemmas/LemmaBulkContracts.cs`, `components/LemmaBulkEdit.tsx`, `lib/lemmas/bulk*.ts`). It also has to fit roadmap `0005-mapping-redesign.md` (Mapping v2: provenance, confirmations, compat views).

**Question:** a bulk replace matches by German lemma (+ optional PoS / `PlainWord`). Can the Elb1871 → Greek/Hebrew mapping keep it from changing occurrences that look the same in German but are a different word underneath?

**Short answer:** yes, as a **grouping and preselection aid**, not as a hard filter. A different Strong's number is often a synonym (Herr = אָדוֹן / אֲדֹנָי / κύριος) and the replace is still right. For real homographs (*Weise* sage/manner, *Tor* gate/fool, *meinen* verb/"my", *sein* verb/"his", *der* article/relative, *Erbe* heir/inheritance) the mapping splits the list cleanly, which the German PoS and gender cannot do. In the OT the signal is weak for function words, because the Hebrew mapping is per word, not per morpheme.

## 1. What the dev DB says (read-only, `postgres001`, 2026-11-12)

All numbers come from `SELECT` / `EXPLAIN ANALYZE` inside `BEGIN READ ONLY`.

### Schema facts that matter

| Fact | Consequence |
|---|---|
| `Elb1871GreekMapping` 170 071 rows, `Elb1871HebrewMapping` 556 416. One row per (`ElbWordId`, `StepWordId`), `StepWordId NULL` + `IsAddedWord` = German word with no source word. | The "original key" of a German word comes from `StepGreekWords` / `StepHebrewWords` through `StepWordId`. |
| Greek: `StepGreekWords.DisambiguatedStrongs` (`G2424G`), `Lemma` (`Ἰησοῦς`), `Morphology` (`N-GSM-P`). `Elb1871GreekMapping.StrongsNumber` is stale in 3 113 rows (NULL although a step word exists). | Read the Strong's number from the step word, never from `Elb1871GreekMapping.StrongsNumber`. |
| Hebrew: `DisambiguatedStrongs` holds **all morphemes**, with the main word in braces: `H9005/{H0935P}`, `{H3068G}\H9016`, `H9005/{H7122G}/H9038`. `Grammar` is split the same way (`HR/Vqcc/Sp3mp`). There is no Hebrew `Lemma` column. Gloss/lemma come from `StepStrongsLexicon` (`LanguageId` 0 = Hebrew, 2 = Greek). | The main Strong's is the braced part. The PoS class is the `Grammar` segment at the same index (language letter `H`/`A` only on the first segment). Prefixes (`H9002` and, `H9009` the, `H9003` in, `H9005` to …) and suffixes (`H9020`–`H9038`) sit in the same string. |
| The mapping has **no author, no AI marker, no confirmation, no version**. `CreatedAt` falls on 3 (Greek) / 5 (Hebrew) days, i.e. batch time. `SaveVerseMapping` deletes and reinserts a verse with `now()`. | Today "AI vs. human" cannot be shown per row. A stale check must compare **content** (the key), not ids or timestamps. Mapping v2 (`MappingLinkTrust`, `MappingConfirmations`) adds the provenance later. |
| `Elb1871GreekMappingTest` / `Elb1871HebrewMappingTest` are not referenced by any C# code and differ from the live tables in 926 / 2 497 edges. | Ignore them. They look like an older snapshot. |
| `SaveVerseMapping` is `AllowAnonymous()`, gated only by the `MappingEditing:Enabled` setting. | The data the bulk guard relies on can be changed without login. That should be `RequiresLogin` now that auth exists (separate fix). |
| `Elb1871MorphologyEdits` 641 rows, `Elb1871MorphologyChangeSets` 2. `Jesu`/`Jesum` are already merged into `Jesus` (939). | The bulk edit is already in use, so the guard is not theoretical. |

### Coverage (German words with a morphology row, by testament)

| | NT | OT |
|---|---|---|
| Elb1871 words | 169 885 | 552 868 |
| aligned to ≥ 1 source word | 137 670 (**81.0 %**) | 543 861 (**98.4 %**) |
| added-only (no source word) | 32 214 (19.0 %) | 8 563 (1.5 %) |
| no mapping row at all | 0 | 336 |
| several source words | 27 | 2 878 |

By German PoS (share aligned):

| PoS | NT | OT | PoS | NT | OT |
|---|---|---|---|---|---|
| NOUN | 97.8 % | 99.9 % | ADP | 71.0 % | 98.7 % |
| PROPN | 99.4 % | 99.9 % | AUX | 60.1 % | 97.5 % |
| VERB | 98.1 % | 99.9 % | PRON | 57.3 % | 98.0 % |
| ADJ | 93.7 % | 99.7 % | DET | 74.3 % | 95.9 % |

Content words are almost fully covered in both testaments. The NT gaps are the German additions (articles, auxiliaries, pronouns the Greek does not have). The OT's high numbers for function words are misleading: there the German "der", "in", "dein" gets the Hebrew word it belongs to (`der` → `H0853` אֵת 3 273×, `H1121` son 2 920×; `in` → `H0776` land 464×; `dein` → `H0430` God 352×), because a Hebrew word carries prefixes and suffixes and the mapping has no morpheme slot.

### Reliability (misalignment proxy)

German content words mapped to a source word of a function class (Greek `T`, `CONJ`, `PREP`, `PRT`, `COND`; Hebrew main morpheme `R`, `C`, `T`, `D`):

| German PoS | NT | OT |
|---|---|---|
| NOUN | 92 / 26 970 (0.34 %) | 524 / 111 194 (0.47 %) |
| PROPN | 32 / 5 408 (0.59 %) | 204 / 30 500 (0.67 %) |
| VERB | 45 / 22 097 (0.20 %) | 1 478 / 64 370 (2.3 %, mostly *siehe* → הִנֵּה) |
| ADJ | 31 / 2 308 (1.3 %) | 33 / 6 682 (0.5 %) |

So the alignment of content words is good (≈ 99 %). The noise is a long tail of 1–5 occurrences per lemma (`Gott` → ὁ 13×, `Jesus` → ὁ 5×, `Geist` → καί 1×). The tail must not block a replace, but it is exactly what the review should show last and flagged.

### Distribution per German lemma (key = base Strong's of the aligned word)

| German lemma | NT | OT | Reading |
|---|---|---|---|
| `Jesus` (939, PROPN) | G2424 928 · G3588 5 · G2962 3 · G0846 2 · added 1 | – | One word; the rest is mapping noise. |
| `Geist` (NOUN) | G4151 363 · 4 singletons | H7307 200 · H7308 10 (Aramaic) · 5 singletons | One concept per language. |
| `Herr` (NOUN) | G2962 722 · G1203 δεσπότης 5 · G2316 3 · … | H0136 אֲדֹנָי 433 · H0113 אָדוֹן 329 · H1167 7 · H3068 4 · H3069 4 | **Synonyms**: replacing all is right, but the groups tell *Herr* (Adonai) from *Herr* (adon) if Elb ever distinguishes them. YHWH is `Jahwe` (4 858 NOUN + 1 642 **PROPN**: a PoS inconsistency the PoS filter would split wrongly). |
| `Gott` | G2316 1 292 · G3588 13 · added 9 | H0430 2 375 · H0410 221 · H0426 80 | Synonyms + noise. |
| `Götter` (own lemma!) | G2316 8 · G1140 1 | H0430 193 · H0426 15 · H0312 3 | Probably a lemma error (`Götter` → `Gott`), and the mapping confirms that it is the same word. |
| `der` (68 777: DET 67 810, PRON 967) | ὁ G3588 10 784 · **ὅς/ὅστις G3739/G3748 587** · added 3 720 · οὗτος/ἐκεῖνος 76 · αὐτός 54 | אֵת, בֵּן, … (host words) 50 892; host word with article `H9009` 12 929; with relative `{H0834}` 2 431 | NT: the relative pronoun is visible through the original (507 of the relative uses are tagged **DET**, so the German PoS does not catch them). OT: only אֲשֶׁר is usable. |
| `sein` | V (εἰμί and the verbs it helps) 3 112 · **P αὐτοῦ 871 (DET) + 209 (AUX)** | – | The 209 `sein` tagged AUX that map to αὐτοῦ ("his") are **German PoS errors** the mapping detects. |
| `meinen` (VERB 104 NT) | **P μου G3165 68** · V 22 · S 13 | H5869 eye 42 · H1285 covenant 34 · H8034 name 31 (host words of the suffix) | A **homograph** in the lemma itself: "meinen" (to mean) vs inflected "mein". Bulk `meinen` → `mein` is right for the 68 P rows and wrong for the 22 V rows. |
| `Weise` (NOUN) | σοφός G4680 9 · τρόπος G5158 4 | חָכָם H2450 32 · H2449 19 · מִשְׁפָּט H4941 14 | **Homograph**: sage vs manner. All tagged NOUN, so the PoS filter cannot separate them. |
| `Tor` (NOUN) | πύλη G4440 17 · G0878 5 | שַׁעַר H8179 gate 252 · **כְּסִיל H3684 fool 63** · H5036 6 | **Homograph** gate / fool. Both have `Gender = N` in the morphology (the fool should be masc.), so gender does not help either. |
| `Erbe` | κληρονόμος G2818 heir 15 · κληρονομία G2817 inheritance 10 | H5159 12 · H5157 9 | **Homograph** (der Erbe / das Erbe). |
| `Arm` (NOUN) | – | H2220 arm 69 · **H7326 be poor 6** (`Arme`) | Lemma error for 6 rows ("die Armen" → `Arm`): exactly the minority group a bulk fix `Arm` → `arm` should target, and only that group. |

### Top 200 most polysemous (lemma, language) pairs

Ranked by `occurrences × (1 − share of the top key)`, at least 50 aligned occurrences and at least two keys with ≥ 5 % each (`poly.txt`, query in the appendix):

- 163 distinct lemmas, 143 Hebrew pairs and 57 Greek.
- **97 are function words / auxiliaries / pronouns** (`der`, `in`, `er`, `ein`, `ich`, `dein`, `an`, `es`, `auf`, `mit`, `sein`, `werden` …). In Hebrew their key is the host word (see above), so it is meaningless. In Greek it is meaningful (`zu` = πρός/εἰς/τοῦ + inf., `daß` = ὅτι/ἵνα, `nicht` = οὐ/μή), but those are translation choices, not German homographs.
- **66 are content words**: `Ende, Fürst, Fuß, ganz, Gerechtigkeit, Geschlecht, Gott, groß, Hand, Haus, Heer, heilig, Herr, Herz, hoch, Höhe, jung, König, Land, Leben, Mann, Menge, Mensch, Mitte, Recht, Seite, Stamm, Tor, Werk, Zeit` and others. Most are synonym sets (`Haus` = בַּיִת / οἶκος / οἰκία; `König` = H4428 / Aramaic H4430 / the verb H4427 "König werden"). Real homographs are the minority (`Tor`, `Weise`, `Erbe`, `Arm`, `Recht` *Recht/recht*).
- A **different original PoS class** within a German content lemma is also mostly translation technique (`Buße` ← μετανοέω V 31×, `Zeugnis` ← μαρτυρέω V 26×, `sehen` ← ἰδού INJ 233×). The cases where it marks a German homograph or tagging error are the pronoun/verb clashes: `meinen` V/P, `sein` AUX/P, `ihr` PRON/V.

**Conclusion from the data:** "this lemma maps to several Strong's numbers" fires for almost every frequent lemma and cannot be a hard stop. It is a strong signal when the minority group is (a) a different original PoS class from the German PoS, or (b) a separate lemma with its own gloss that the user recognises at a glance (gate / fool). The UI has to put the original word and its gloss on the screen and let the human decide by group.

## 2. Recommendation

### 2.1 Signal

One derived value per Elb word, the **original key**, computed in one place (a SQL fragment / later a view on the Mapping v2 compat views):

| Field | Greek | Hebrew |
|---|---|---|
| `lang` | `G` | `H` |
| `kind` | `aligned` / `added` (`StepWordId NULL` + `IsAddedWord`) / `none` (no row) / `multi` (several step words, key = `+`-joined) | same |
| `strongsBase` (**group key**) | `G\d+` of `DisambiguatedStrongs` (`G2424`) | braced main morpheme, base number (`H0430`) |
| `strongs` (shown) | `G2424J` | `H0430J` (braced part) |
| `origWord` / `origLemma` / `gloss` | `Greek`, `Lemma`, `StepStrongsLexicon.Gloss` | `Hebrew`, `StepStrongsLexicon.OriginalWord` / `Gloss` |
| `origPos` (class) | `split_part(Morphology, '-', 1)`: N V A T R P D … | `Grammar` segment at the braced index, without the leading `H`/`A`, first letter: N V A R C T S … |
| `affixes` (Hebrew only) | – | the other morphemes (`H9009` the, `H9002` and, `H9020`–`H9038` suffixes) |

How the signals combine:

1. **Group by `(lang, strongsBase)`**, not by the disambiguated Strong's (`G2962G`/`G2962H` are the same κύριος) and not by the original lemma text (Hebrew has none, Greek spells it with variants such as `ὅς, ἥ`). The base number is stable, indexed (`IX_StepGreekWords_DisambiguatedStrongs`, `IX_StepHebrewWords_RootStrongs`) and already the key of `GetStrongsOccurrences`.
2. **Inside an original group keep the current `(PlainWord, PartOfSpeech)` subgroups.** The German PoS stays a filter; the original does not replace it.
3. **PoS agreement as a flag, not as a key.** A static table maps German UPOS to the expected original classes (NOUN/PROPN → N (+A); VERB/AUX → V; DET → T, D, P (possessive), R; PRON → P, R, D, F; ADJ → A, N; ADP → PREP / Hebrew R; …). A group whose `origPos` is outside the expected set gets "Wortart weicht ab" (`meinen` VERB → P, `sein` AUX → P).
4. **Minority flag**: a group with < 5 % of the aligned occurrences of its language (and not the top key) is "selten" (`Gott` → ὁ, `Jesus` → κύριος).
5. **Language matters**: G and H keys are never compared with each other. `Geist` = H7307 + G4151 is one word, not two.
6. **Function words in the OT**: when the German PoS is DET/ADP/CCONJ/SCONJ/PRON/AUX/PART and `lang = H`, the main-morpheme key is the host word. Show the key but **do not flag** minority/PoS conflicts there, and offer the affix (`H9009` / `{H0834}`) as the only Hebrew criterion. This avoids hundreds of false warnings for `der`.

Not used: German gender (`Tor` fool is tagged neuter), the stale `Elb1871GreekMapping.StrongsNumber`, `CreatedAt`.

### 2.2 UI

**Step 1 (Suchen)**

- New `Original` filter next to `Wortart`: a `Select` with the original groups of the current "from" (filled from `byOriginal`, e.g. `G4680 σοφός "weise" 9 · G5158 τρόπος "Art" 4 · H2450 … · ohne Original 0`), multi-value through repeated `original=` keys like `from`. Values `added` / `none` are selectable too.
- **Ambiguity hint** under "Von" when, within one language, ≥ 2 content groups have ≥ 10 % each **and** different glosses, or a PoS-conflict group exists: "»Weise« steht für 4 verschiedene Originalwörter – Liste nach Original gruppiert, seltene Gruppen sind abgewählt." Plain text, `accent` with an "!" glyph, never colour alone.

**Step 2 (Prüfen)**

- A **grouping switch** `Gruppieren: [Form] [Original]` (Button `aria-pressed`). "Original" is the default when the ambiguity hint fired, otherwise "Form" (today's view). With "Original", a two-level list: original header → `(PlainWord, PoS)` subgroup → rows. Both levels are tri-state options of the listbox.
- **Original header**: `☑ ▾ G5158 τρόπος · "Art, Weise" · N · 4` plus flags `selten` / `Wortart weicht ab` / `KI` (later, Mapping v2 trust) / `ohne Original`.
- **Each row** gets an original column: `σοφός` (word as in the verse) + `G4680`. Hebrew shows the word and the braced Strong's. `added` shows "– (ergänzt)", `none` shows "– (nicht zugeordnet)". A link "Zuordnung" opens the verse in the mapping editor in a new tab, so a wrong mapping is fixed at the source instead of being worked around.
- **Default selection:** everything selected **except** groups flagged `selten` or `Wortart weicht ab` (only when the hint fired; with a single dominant key nothing changes). `added` / `none` groups stay selected for function words (they are the normal case there) and are deselected for NOUN/PROPN/VERB/ADJ lemmas, where they mean a mapping gap. The selection model already supports group defaults (`Selection.groups`), so this is a precomputed `groups` map, not a new mode.
- The selection counter adds the breakdown: "312 von 340 ausgewählt · 3 Originalgruppen abgewählt".
- **Unmapped words**: never hidden, never silently excluded. In the NT ≈ 19 % of all words are `added`, in nouns only 2 %. They form their own group at the end. With Mapping v2, AI-authored links get a `KI` chip and "unbestätigt" in the tooltip; a filter "nur bestätigte Zuordnungen" is possible then, not now.

**Step 3 (Fertig)**: the result line names the original groups that were left out ("τρόπος (4) nicht geändert"), with the "Weiter" suggestion "Für diese 4: anderes Lemma setzen…", which reopens step 1 with `original=G5158` preset. That is the natural way to *split* a homograph (`Weise` → `Weiser` for σοφός, keep `Weise` for τρόπος).

### 2.3 Backend contract

All additive; old clients keep working.

**`BulkFilterRequest`** (occurrences, word-ids, and so apply's preview):

```csharp
/// <summary>Original-language group keys: "G4151", "H7307", "added", "none". Repeated query keys, OR-combined.</summary>
public string[] Original { get; set; } = [];
```

Validation: each value matches `^(G|H)\d{4}$|^added$|^none$`, at most 20.

**`BulkOccurrence`** gets (nulls omitted):

```csharp
public string? OrigKind { get; set; }    // aligned | added | none | multi
public string? OrigKey { get; set; }     // G4680 / H2450 (group key)
public string? OrigStrongs { get; set; } // G4680 / H2450G (disambiguated, shown)
public string? OrigWord { get; set; }    // surface form in the verse
public string? OrigPos { get; set; }     // N V A T R P …
public bool PosConflict { get; set; }
```

**`BulkOccurrencesResponse`** gets `ByOriginal` (over all matches, like `ByBook`):

```csharp
public sealed class BulkOriginalCount
{
    public string Key { get; set; } = "";     // G4680 | added | none
    public string? Lang { get; set; }
    public string? Lemma { get; set; }        // σοφός / חָכָם (StepStrongsLexicon.OriginalWord)
    public string? Gloss { get; set; }
    public string? Pos { get; set; }          // dominant class of the group
    public long Count { get; set; }
    public bool Minority { get; set; }
    public bool PosConflict { get; set; }
}
public bool Ambiguous { get; set; }           // drives the hint and the default grouping
```

`Groups` becomes `(OrigKey, PlainWord, PartOfSpeech)` triples when the request has `groupBy=original`; `groupKey()` in `bulk.ts` adds the third part, and `BulkWordIds` with `original=` + `plainWord` + `partOfSpeech` fetches one subgroup. The flags are computed in C# (`LemmaBulkRules`) from `ByOriginal`, so they are unit-testable without SQL.

**Apply-time stale check on the mapping.** `BulkApplyRequest` gets an optional parallel array:

```csharp
/// <summary>Same length and order as WordIds: the OrigKey the user saw ("" = added/none). Omitted = no mapping guard.</summary>
public string[]? ExpectedOriginal { get; set; }
```

In the existing transaction, after `LockAndReadSql`, the server recomputes the key for the ids (same SQL fragment) and compares. A mismatch is stale like a lemma change: 409, nothing written. `BulkStaleResponse` keeps `Stale` (ids) and adds `Details: [{ wordId, reason: "lemma" | "original", lemma?, origKey? }]`, so the client can say "Zuordnung inzwischen geändert (jetzt τρόπος)". The mapping rows are not locked: with one maintainer and the mapping editor writing per verse, a race inside the few milliseconds of the transaction is acceptable. If it ever matters, `FOR SHARE` on the mapping rows of the ids closes it.

The change set `Scope` JSON records `original` filter and the kept/dropped group keys. Optionally a nullable `OriginalKey varchar(12)` on `Elb1871MorphologyEdits` (migration 0011) keeps the key per row for later reports ("all `Weise` edits of σοφός"). The audit `BeforeValues`/`AfterValues` shape stays as it is.

**Should later mapping edits invalidate a review?** Only rows whose key changed, and only at apply time (above). Re-keying a word does not make an *applied* lemma change wrong: the lemma is a German fact. So there is no retroactive invalidation of change sets. Mapping v2 can list "lemma edits whose original key changed since" as a review query, a report, not a lock.

**Performance (`EXPLAIN ANALYZE`, dev DB, current indexes):**

| Lemma | Rows | Plan | Time |
|---|---|---|---|
| `Weise` | 98 | Bitmap on `Elb1871Morphology_Lemma_idx` → nested loop, index scans `UqElbStepGreek` / `UqElbStepHebrew` → step-word PKs | 7 ms |
| `Herr` | 1 523 | same | 55 ms |
| `sein` | ≈ 26 000 | same (LATERAL form) | 765 ms |
| `der` | 68 777 | LATERAL form: nested loop, 716 k buffer hits | 1 020 ms |
| `der` | 68 777 | UNION ALL per language + hash join on `ElbWordId` (seq scan of mapping + step words) | 466 ms |

- **No new index needed.** `UqElbStep*` (leading `ElbWordId`) and the step-word PKs serve the selective case; the big lemmas switch to hash joins. Write the SQL as "matched ids → UNION ALL Greek/Hebrew → join", not as a LATERAL per row.
- The real cost is that `OccurrencesSql` recomputes the `Matched` CTE five times **per page**. With the mapping join that would be 5 × 0.5 s for `der`. Compute the aggregates (`total`, `byBook`, `byLemma`, `groups`, `byOriginal`) in one statement per search (one `MATERIALIZED` CTE, `json_agg` per result set, or a separate `GET bulk/summary` that React Query caches per filter) and join the original only for the ≤ 500 rows of the page.
- A precomputed `Elb1871WordOriginalKeys` table (refreshed by `SaveVerseMapping` per verse and by the seed) would make this ~free, but it duplicates Mapping v2. Wait for 0005 and derive the key from its compat views (`Elb1871GreekMappingCompat` / `Elb1871HebrewMappingCompat`) and `MappingLinkTrust`.

## 3. Wireframe delta

```
┌ Lemma ersetzen ─────────────────────────────────────────────── ✕ ┐
│ Von  [Weise         ] 98×       Nach [Weiser        ] neues Lemma │
│ ! »Weise« steht für 4 Originalwörter (σοφός, τρόπος, חָכָם, מִשְׁפָּט).│
│   Liste wird nach Original gruppiert, seltene Gruppen abgewählt.   │
│ Bereich (●Ganze Bibel ○AT ○NT ○Buch …)                             │
│ Wortart [alle ▾]   Original [alle ▾]  ← NEU (G4680 σοφός 9, …)     │
│                                          [Abbrechen] [Vorkommen ▸]│
└───────────────────────────────────────────────────────────────────┘

┌ Weise → Weiser · Ganze Bibel · 98 Treffer ✎ ──────────────────── ✕ ┐
│ Gruppieren: [Form] [■Original]            Bücher: [Spr 20] [Pred 9]…│
│ [☑ alle] 60 von 98 ausgewählt · 2 Originalgruppen abgewählt         │
│ ☑ ▾ H2450 חָכָם »weise« · A/N · 32                                   │
│ ☑   ▸ weise · NOUN · 18                                             │
│ ☑       Spr 1,5   Der [Weise] wird hören …        חָכָם  H2450  ⌄  │
│ ☐ ▾ H4941 מִשְׁפָּט »Recht, Weise« · N · 14   ! andere Bedeutung        │
│ ☐       1Mo 40,13 nach der früheren [Weise], da …  מִשְׁפָּט H4941G ⌄  │
│ ☑ ▸ H2449 »weise sein« · V · 19 (zugeklappt)                      │
│ ☑ ▾ G4680 σοφός »weise« · A · 9                                      │
│ ☐ ▾ G5158 τρόπος »Art« · N · 4                ! andere Bedeutung    │
│ ☐ ▾ – ohne Original · 0                                              │
│                           [Zurück] [60 Wörter ändern (Strg+Enter)] │
└───────────────────────────────────────────────────────────────────┘

┌ Lemma ersetzen ─────────────────────────────────────────────── ✕ ┐
│ ✓ 60 Wörter: Weise → Weiser.  Nicht geändert: מִשְׁפָּט (14), τρόπος (4) │
│ Weiter: diese 18 [anderes Lemma setzen …]   [Rückgängig] [Schließen]│
└───────────────────────────────────────────────────────────────────┘
```

(Counts from the dev DB: H2450 32, H2449 19, H4941 14, G4680 9, G5158 4; selected 60, left out 18; the remaining 20 rows sit in small groups (H2063 6 …) not drawn here. The flags are illustrative; "andere Bedeutung" is the user-facing text for `Minority` with a different gloss, `Wortart weicht ab` for `PosConflict`.)

## 4. Risks and open questions

**Risks**

- **False confidence.** The mapping is AI-made and unconfirmed. A deselected minority group can be a mapping error, not a different word, so the dropped rows must stay visible in the result step, and "Zuordnung" must be one click away.
- **Warning fatigue.** Almost every frequent lemma has several keys. Firing the hint on "≥ 2 keys" would make it useless; it needs the gloss/PoS conditions above and must stay silent for OT function words.
- **Hebrew granularity.** The mapping says *which Hebrew word*, not *which morpheme*. For `der`, `und`, `in`, `dein`, `mir` the OT key is the host word. Real protection there needs Mapping v2 with morpheme-level links (or at least the affix list as a filter).
- **Synonym groups deselected by default** (`Herr` H0136/H0113) would silently shrink a correct replace. Hence: deselect only when the hint fires, and only `Minority`/`PosConflict` groups, never the second-largest group just because it differs.
- **Cost on big lemmas.** Without the one-pass summary, `der`/`und`/`sein` pages get ≈ 2–3 s slower.
- **Stale check by content** misses a mapping change that keeps the same key but changes the source word (e.g. another κύριος in the verse). Harmless for a lemma edit.
- **Anonymous mapping writes** (`SaveVerseMapping`) undermine the guard. Switch to `RequiresLogin` with the roles of the bulk edit.

**Open questions**

1. Default grouping: always "Original" when any mapping exists, or only when `Ambiguous`?
2. Thresholds: minority < 5 % and ambiguity ≥ 2 groups with ≥ 10 % each: good enough, or configurable per run?
3. Should `added`/`none` rows of content lemmas be deselected by default (safer) or selected (Jesu/Jesum-style fixes are form errors that don't depend on the original)?
4. Is `ExpectedOriginal` opt-in per apply (only when the user grouped by original), or always sent?
5. Store `OriginalKey` per edit row (migration 0011), or is the change set `Scope` enough?
6. Wait for Mapping v2 (trust/confirmation, morpheme links) before building this, or build v1 now on the current tables behind one SQL fragment and swap it to the compat views later? (Recommended: build now; the derivation is ~30 lines of SQL.)
7. Should the review offer "Zuordnung korrigieren" inline (mapping editor in the dialog), or only a link?
8. The data exposes German tagging errors (`sein` AUX → αὐτοῦ 209×, `Jahwe` NOUN/PROPN split, `Götter` as its own lemma, `Tor` fool tagged neuter). Should the lemma editor get a "Konflikte mit dem Original" report as its own entry point, separate from the bulk replace?

## Appendix: queries

The key per Elb word (as used for all numbers above):

```sql
SELECT g."ElbWordId"
     , CASE WHEN s."Id" IS NULL THEN CASE WHEN g."IsAddedWord" THEN 'added' ELSE 'none' END
            ELSE substring(s."DisambiguatedStrongs" FROM 'G\d+') END AS "OrigKey"
     , s."DisambiguatedStrongs", s."Lemma", split_part(s."Morphology", '-', 1) AS "OrigPos"
FROM "unshackled-word"."Elb1871GreekMapping" g
    LEFT JOIN "unshackled-word"."StepGreekWords" s ON s."Id" = g."StepWordId"
UNION ALL
SELECT h."ElbWordId"
     , CASE WHEN s."Id" IS NULL THEN CASE WHEN h."IsAddedWord" THEN 'added' ELSE 'none' END
            ELSE COALESCE(substring(s."DisambiguatedStrongs" FROM '\{(H\d+)'), substring(s."DisambiguatedStrongs" FROM 'H\d+')) END
     , COALESCE(substring(s."DisambiguatedStrongs" FROM '\{([^}]+)\}'), s."DisambiguatedStrongs"), NULL
     , (SELECT left(regexp_replace((string_to_array(s."Grammar", '/'))[i], CASE WHEN i = 1 THEN '^[HA]' ELSE '^$' END, ''), 1)
        FROM unnest(string_to_array(s."DisambiguatedStrongs", '/')) WITH ORDINALITY u(x, i) WHERE x LIKE '{%' LIMIT 1)
FROM "unshackled-word"."Elb1871HebrewMapping" h
    LEFT JOIN "unshackled-word"."StepHebrewWords" s ON s."Id" = h."StepWordId"
```

Polysemy ranking: group `lemma × lang × OrigKey` (aligned only), keep pairs with ≥ 50 occurrences and ≥ 2 keys of ≥ 5 %, order by `total × (1 − topShare)`, limit 200.
