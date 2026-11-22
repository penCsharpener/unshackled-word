# 0023 · API and UI

## Endpoints (FastEndpoints, `Endpoints/Notes/`, contracts in `Domain.WebApi/Notes/`)

| Route | Auth | Purpose |
|---|---|---|
| `GET api/notes/chapter/{bookId}/{chapter}?others=true` | public | Notes of a chapter the caller may see (filter in [03-model-and-anchoring.md](03-model-and-anchoring.md)); `others=false` returns only own notes. Each with anchor, `anchorState`, `bodyLinked`, author. Anonymous: rate-limited like the other reads |
| `GET api/notes/{id}` | public (filtered) | One note, 404 if not visible to the caller |
| `POST api/notes` | login | Create; validator checks anchor shape, verse existence, positions against `Elb1871Words`, fills `QuotedText` server-side |
| `PUT api/notes/{id}` | owner | Body and anchor; on an approved public note sets `PublicState = pending` |
| `PUT api/notes/{id}/visibility` | owner | `private` / `internal` / `public` (= request) |
| `DELETE api/notes/{id}` | owner | Hard delete (own data, confirm dialog in the UI) |
| `GET api/notes/mine?book=&visibility=&q=&skip=&take=` | login | List page; `q` is `ILIKE` on the body |
| `GET api/notes/mine/export` | login | Own notes as JSON file download (format of [06-export-import.md](06-export-import.md)) |
| `GET api/maintainer/notes/review` | maintainer | Pending and hidden notes |
| `POST api/maintainer/notes/{id}/approve` · `/reject` · `/hide` · `/unhide` | maintainer | Review actions; reason for reject/hide; admin log entry |
| `POST api/maintainer/notes/export` · `/import` | maintainer | Background jobs, see [06-export-import.md](06-export-import.md) |

Validation with FluentValidation; ownership checked in the endpoint via the session user id, never from the request body.

## Reader (`ChapterPage`)

- **Markers.** A small note icon (`NoteMarker`, design-token sized, `aria-label` "Notiz von …") floats at the end of an anchored word range (superscript, does not shift the line), at the verse number for verse and verse-range notes (range: on the first verse, with a thin bracket line in the verse gutter on wide screens), at the chapter heading for chapter notes. Own notes and others' notes differ by token colour; several notes on one anchor show one icon with a count.
- **Side panel mode `notes`** (fourth value of `SidePanelMode`, keyboard toggle cycles through it): lists the chapter's notes in reading order, the clicked one scrolled into view and expanded. On a phone it opens as the existing bottom sheet.
- **Creating.** Select words in one verse with the mouse/touch (start and end word snap to `WordToken`s) → floating action "Notiz"; the verse number's context action "Notiz zum Vers"; shift-click a second verse number for a range "Notiz zu V. 28–30"; chapter heading action "Notiz zum Kapitel". Opens `NoteEditor` in the side panel with the anchor shown as a quote/reference line.
- **Toggle** "Notizen anderer anzeigen" in the reader settings (stored per viewer like the other reader settings); default on.
- Signed-out readers see public notes and no create actions; a click on "Notiz" asks to sign in.

## Components (propose in chat before building, register in `Components.md`)

- `NoteEditor` — MDXEditor with toolbar: bold, italic, heading (H3/H4), bulleted/numbered list, quote, link, inline code, undo/redo; Markdown source toggle for people who know the syntax. Visibility select (private/internal/public with the state badge), save (Ctrl+S, like the vocab editor), cancel, delete. Lazy-loaded chunk so the reader bundle does not grow for readers who never write.
- `NoteView` — read-only rendering of `bodyLinked` ([04-references.md](04-references.md)), author line, date, visibility badge, drift hint.
- `NoteMarker` — the floating icon.
- Reuse: `PassagePreviewCard`, `SaveBar` (if it fits the side panel), existing badge/button components.

## Pages

- `/notes` "Meine Notizen" (header user menu): list with book filter, visibility filter, search, excerpt, reference as link to the passage with the note opened (`/bible/45/8?v=28&note=<id>#v28`), button "Meine Notizen herunterladen".
- `/edit/notes` "Notizen prüfen" (maintainer menu): pending queue with diff of approved vs. current text, approve/reject with reason, hidden list with unhide.
