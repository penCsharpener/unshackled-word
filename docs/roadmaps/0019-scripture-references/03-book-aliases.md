# 0019 · Book alias catalogue

New static class `BookAliasCatalog` in `src/UnshackledWord.Domain/Models/BibleStructure/References/`. It is the only place that turns a book token into a book id. `GermanBookNames` and `BibleBook.AllBooks` stay the sources of names (display, `structure` endpoint); the catalogue reads them and adds what is missing.

## Profiles

A profile is a named alias set. The parser is called with one profile; the scanner with another.

| Profile | Used by | Content |
|---|---|---|
| `German` | search overlay, `references/parse` | `GermanBookNames` names + abbreviations, plus Loccum forms (`Gen`, `Ex`, `Dtn`, `Ri`, `1 Kön`, `Ijob`, `Koh`, `Hld`, `Klgl`, `Ez`, `Obd`, `Jona`, `Zef`, `Phlm`, `1 Thess`, `Hebr`, `Offb`), Elberfelder spellings (`Hohelied`, `Klagelieder Jeremias`), and full names without number dot (`1 Mose`) |
| `Legacy` | only the old `BibleReferenceParser.Parse` wrapper (MCP) | `German`, then `BibleBook.AllBooks` English names + abbreviations as fallback — exactly today's lookup order, so `John 3:16` keeps working |
| `Osis` | lexicon | STEP/OSIS codes: `Gen Exo Lev Num Deu Jos Jdg Rut 1Sa 2Sa 1Ki 2Ki 1Ch 2Ch Ezr Neh Est Job Psa Pro Ecc Sng Isa Jer Lam Ezk Dan Hos Jol Amo Oba Jon Mic Nam Hab Zep Hag Zec Mal Mat Mrk Luk Jhn Act Rom 1Co 2Co Gal Eph Php Col 1Th 2Th 1Ti 2Ti Tit Phm Heb Jas 1Pe 2Pe 1Jn 2Jn 3Jn Jud Rev`, plus the variants found in the dev DB: `Ezr`, `Jude`, `Luke`, `Mark`, `John`, `Matt`, `Acts`, `Phil`, `1Cor`, `1Jo`, `1Tim`, `1Pet` |

Non-canonical codes (all profiles, result "known, not linkable"): `Sir Wis Tob Jdt 1Ma 2Ma 3Ma 4Ma 1Mac 2Mac 3Mac Bar Sus Bel 1Es 2Es Man PSS Pss 3Ki 4Ki` (`3Ki`/`4Ki` are LXX "Kingdoms" with LXX counting, not safely mappable) and German `Sir Weish Tob Jdt 1Makk 2Makk Bar`.

Result type:

```csharp
public readonly record struct BookMatch(int? BookId, string? NonCanonical, BookMatchKind Kind); // Exact, Prefix, NonCanonical
public static BookMatch? Resolve(string token, AliasProfile profile, bool allowPrefix = false);
public static IReadOnlyList<int> Candidates(string token, AliasProfile profile); // for "ambiguous" messages
```

## Normalisation (one function, used for aliases and input)

1. Unicode NFC, trim, lower-case invariant.
2. Leading ordinal: `1.`, `1 `, `1`, `I`, `II`, `III`, `i.` → `1`/`2`/`3` (Roman only when followed by space or dot and a letter, so a book token starting with `I` is not cut).
3. `ä→ae`, `ö→oe`, `ü→ue`, `ß→ss` (so `Röm`, `Roem`, `ROEM` are equal; already done by `GermanBookNames.Normalize`).
4. Remove spaces, dots and `-`.

Examples: `1. Kor.` → `1kor`; `I Kor` → `1kor`; `II Sam` → `2sam`; `Offenbarung` → `offenbarung`; `Hohes Lied` → `hoheslied`.

Index: `Dictionary<string, int>` per profile (normalised alias → book id), built once in a static constructor. A duplicate key within one profile with two different books throws at type init → caught by the first test.

## Prefix matching (search only)

When there is no exact alias and `allowPrefix` is set, the token (≥ 3 normalised characters) is matched as prefix of every normalised **full name** and alias of the profile:

- exactly one book → `Kind = Prefix` (`Offen` → 66, `Apost` → 44, `Matth` → 40);
- several books → no match; the parse result carries the candidates (`Phili` → Philipper, Philemon), shown in the card as "Meinten Sie …".

Prefix matching is never used by the lexicon scanner.

## Collisions to pin down in tests

Within `German` there must be none. Across profiles the same token can mean different books; the tests pin these:

| Token | `German` | `Osis` | Note |
|---|---|---|---|
| `Jud` | Judas (65) | Jude (65) | Judges is `Jdg` in STEP; `jud` was removed from Judges in e54b30c |
| `Ri` | Richter (7) | – | |
| `Hes` / `Ez` | Hesekiel (26) | `Ezk` = 26 | |
| `Pred` / `Koh` | Prediger (21) | `Ecc` = 21 | |
| `Hl` | Hoheslied (22) | `Sng` = 22 | |
| `Jak` | Jakobus (59) | `Jas` = 59 | |
| `Phil` | Philipper (50) | Philippians (50, variant) | |

In `Legacy`, a German alias always wins over an English one (today's order).
