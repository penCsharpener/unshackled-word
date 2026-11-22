# 0023 · Goal

## Missing today

A reader can study a chapter (original text, TSK, lexicon, references), but cannot write down what they found. There is no user-owned content in the app at all besides vocab progress (`Vocab*`): no table, no endpoint, no editor for free text. The only rich text rendering is `ScriptureText` (lexicon segments), and the reference scanner (`ScriptureScanner`) only recognises STEP/OSIS notation (`1Ch.8.39`), not German citations like `Röm 8,28`.

## End state

A logged-in user writes Markdown notes (toolbar editor, `@mdxeditor/editor`) attached to one of four anchors in the Elb1871 text:

| Anchor | Example | Precision |
|---|---|---|
| chapter | Röm 8 | whole chapter |
| verse range | Röm 8,28–30 (one chapter) | verses |
| verse | Röm 8,28 | one verse |
| words | „denen, die Gott lieben" in Röm 8,28 (one verse) | word range or a single word |

- Every note has one of three visibility levels: **private** (default, only the author), **internal** (all logged-in users, no approval) and **public** (everyone incl. anonymous readers, only after a maintainer approved it). Others see the author's display name.
- In the reader a tiny note icon floats on a word range or at the verse/chapter; clicking it opens the note in the side panel (new mode `notes`). A reader toggle shows or hides other users' notes.
- Scripture references in a note body (`Röm 8,28`, `1. Mose 1,1–2,3`, `Joh 3`) are rendered as deep links (`/bible/45/8?v=28#v28`) with the existing passage preview.
- `/notes` ("Meine Notizen") lists the own notes: filter by book and visibility, text search, jump to the passage.
- The maintainer exports all notes to `temp/notes/` (bind mount, git-ignored) and imports them back idempotently, e.g. around a seed rebase or a fresh database. Each user can download their own notes as a file.

## Out of scope (deferred)

- Notes across chapter boundaries, word selections across verses (use a verse-range note).
- Notes in MCP read tools, comments/replies on notes, likes, following authors.
- Notes on revision variant wording that has no Elb1871 base word (see [03-anchoring.md](03-anchoring.md)).
- Self-service user import (only the maintainer imports); sharing a private note with chosen users.
- Notes in the repository seed (`assets/`): notes never go into the repo.
