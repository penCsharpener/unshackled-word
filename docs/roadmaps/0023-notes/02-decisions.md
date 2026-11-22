# 0023 · Decisions

## Maintainer decisions (2026-11-22)

- **Four anchors:** chapter, verse range (within one chapter), verse, word range/single word (within one verse). A verse range gives no word precision.
- **Three visibility levels:** `private` (default), `internal` (logged-in users, no approval), `public` (everyone, maintainer approval required).
- **Editor:** Markdown with toolbar, `@mdxeditor/editor` is approved as a dependency. No other new frontend dependency.
- **Export/import:** maintainer exports and imports all notes to/from `temp/notes/` (the existing `./temp:/src/temp` bind mount of `unshackledword.api`), never `assets/`. Users additionally download their own notes.
- **Scripture references** in notes are deep linked automatically.
- **Reader UI:** a tiny note icon floats on the word range or verse range; the list page "Meine Notizen" and a toggle for other users' notes are in scope.

## Decisions proposed by the plan (accepted by the maintainer 2026-11-22)

- **Approval snapshot.** A public note shows anonymous readers `ApprovedBodyMarkdown`, the text the maintainer approved. Editing an approved note sets `PublicState` back to `pending`; until the next approval anonymous readers keep seeing the approved text and logged-in users see the current text (the note is effectively internal for them). This keeps an unreviewed edit from reaching the public under an approved badge.
- **Pending is internal.** Requesting `public` makes the note visible as internal immediately. A rejection (with an optional maintainer reason) leaves it internal; the author may edit and request again.
- **Maintainer hide.** A maintainer can hide any non-private note (`HiddenAt`, reason): it disappears for everyone but the author. Admin log entry like 0020.
- **Raw Markdown is stored, links are computed on read.** The body is saved as typed; references are recognised when the note is served, so a fixed parser improves old notes without a migration. See [04-references.md](04-references.md).
- **No new backend dependency** (no Markdig). The reference pass skips fenced code, inline code and existing Markdown links with a small hand-written tokenizer.
- **Note identity is a uuid** (`gen_random_uuid()`), so export/import across databases needs no id remapping. Authors are matched by `EmailNormalized` on import.
- **Body limit** 20,000 characters, plain Markdown only (no HTML, no images, no MDX/JSX nodes in the toolbar).
- **Version:** this roadmap bumps to 0.9.0.

## Findings the plan rests on

- `Elb1871Words.Id` is not stable: `RebaseRepository` deletes all rows. Anchors therefore use natural keys (`HebRefId`, `PositionInVerse`), as `RevisionVerses` already does (`0020_BibleRevision.sql`).
- `RevisionWords.BasePositionInVerse` maps variant words back to Elb1871 positions, so word anchors can be shown on revision levels.
- `ScriptureReferenceParser` already has a German profile (`api/references/parse`), `ReaderPath` builds the deep link, `PassagePreviewCard`/`ScriptureText` render reference links with preview.
- Users have `DisplayName` (`0008_Auth.sql`), roles `Admin`/`Maintainer` exist, the admin log exists (0020).
- The reader's side panel has modes `tsk | original | off` (`lib/bible/sidePanel.ts`); a fourth mode `notes` fits there.
- Next migration number is `0042`.
