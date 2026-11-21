# 0022 · UI drafts (click dummy → code)

[click-dummy.html](click-dummy.html) is the design to review and the source to copy from. Open it in a browser. The dark bar on top is the dummy's own control: screens 1–7, card type, anonymous, light/dark, and **"Komponenten zeigen"**, which outlines every component with its name (blue = existing, pink = new).

## Copy rules (for whoever implements a `f` row)

1. **Classes are app classes.** Everything inside `<main>` and the dialogs uses the Tailwind classes and tokens of `src/styles/index.css` (the dummy loads the same tokens). Copy `class` into `className` as it is. Do not add colours, arbitrary values or inline styles.
2. **`data-c="Name"`** is the root of a component. With `data-new="1"` it is a new file `src/components/Name.tsx` (props below, register in `Components.md`). Without it, it is an existing component: use it, and do not copy its markup.
3. **`data-btn="variant size"`** → `<Button variant="…" size="…">`. Never a raw `<button>`. Router links that look like buttons use `buttonClasses()`.
4. **`data-uses="…"`** names the existing component and props that replace the raw markup (`TextField`, `Select`, `Switch`, `StatusBadge`, `ProgressBar`, `StatTile`, `RankedBarList`, `SaveBar`, `Dialog`). The raw inputs, switches and `<dialog>` in the dummy are only stand-ins.
5. **`data-page="…"`** is the file that owns the block.
6. German texts in the dummy are the `de.ts` values. Write the `en.ts` value in the same commit. Keys: `vocab.*` (public pages), `learn.*` (study), `vocabEdit.*` (editor), `maintainer.vocab.*` (jobs).
7. Greek text gets `lang="grc"` and `font-original`. Hebrew gets `lang="he"`, `dir="rtl"` and `font-original`.
8. The JS in the dummy is mock behaviour. Copy only the rules that are written in this file (segmenting, normalizing, keyboard order).

## Routes and pages

| Dummy screen | Route | Page file | Auth | Row |
|---|---|---|---|---|
| 1 | `vocab` | `pages/VocabListPage.tsx` | public | 0022.1f, 0022.3f |
| 2 | `vocab/:id` | `pages/VocabDetailPage.tsx` | public | 0022.2f, 0022.3f |
| 3 | `learn` | `pages/LearnPage.tsx` | public; cards need login | 0022.8f, 0022.8bf, 0022.12f |
| 4 | `learn/:course` | `pages/StudySessionPage.tsx` | user (`RequireRole` without role) | 0022.8f–0022.11f |
| 5 | `edit/vocab` | `pages/VocabEditorListPage.tsx` | Admin/Maintainer | 0022.4f |
| 6 | `edit/vocab/:id` | `pages/VocabEditorPage.tsx`, `pages/vocab/KeyMoveDialog.tsx`, `pages/vocab/MergeDialog.tsx` | Admin/Maintainer | 0022.4f, 0022.6f1/f2 |
| 7 | `edit/maintainer` | `pages/VocabJobsSection.tsx` in `MaintainerPage` | Admin/Maintainer | 0022.2f, 0022.3f, 0022.13f |

Header: `AppShell` gets the nav links "Vokabeln" (`/vocab`) and "Lernen" (`/learn`). `UserMenu` gets "Vokabeln bearbeiten" for Admin/Maintainer, next to "Lemmata bearbeiten".

## Changes to existing components

- **`OriginalVerse`**: new optional prop `highlight?: number[]` (word positions). Highlighted words get `rounded-sm bg-mark-linked px-0.5 text-fg` in a `<mark>`. No other change.
- **`RankedBarList`**: no change. Renderings map to `items` as `{ key: germanKey, label: german, sub: "davon n aus 2027", value: linkCount, flags: notAccepted ? [{ label: "nicht angenommen", severity: "info" }] : [] , muted: notAccepted }`. The editor passes `onSelect` = add as meaning.

## New components

Proposed here, to confirm in chat before 0022.1f as CLAUDE.md requires. Each one gets a Vitest file.

```ts
// VocabEntryList.tsx — screen 1 (public) and 5 (editor). Table from md, cards below md.
interface VocabEntryListProps {
  mode: "public" | "editor";
  entries: VocabEntryRow[];   // rank, id, lemma, transliteration, form, formOrigin, meanings[], occurrences,
                              // hasAudio, reviewed, learners (editor only), lang ("grc" | "he")
  linkTo: (id: number) => string;   // "/vocab/:id" or "/edit/vocab/:id"
  onPlay?: (id: number) => void;
}

// AudioField.tsx — screen 2 (readOnly: one play Button) and 6 (editable).
interface AudioFieldProps {
  src: string | null;
  readOnly?: boolean;
  onUpload?: (file: File) => Promise<void>;   // accept="audio/mpeg", 1 MB checked client-side too
  onRecord?: (mp3: Blob) => Promise<void>;     // recorder from Lexio 0020/0029 (lamejs), added in 0022.6f2
  onDelete?: () => Promise<void>;
}
// states: empty | ready | recording | preview | uploading (screen 6 shows all but uploading)

// MeaningListEditor.tsx — screen 6.
interface MeaningRow { id?: number; german: string; count: number | null; share: number | null;
  isAccepted: boolean; origin: "derived" | "manual"; isPinned: boolean }
interface MeaningListEditorProps { rows: MeaningRow[]; onChange: (rows: MeaningRow[]) => void }
// up/down Buttons (ghost sm), pin = Button secondary aria-pressed, accept = Switch, delete only for manual

// StrongsKeyList.tsx — screen 6.
interface StrongsKeyListProps { keys: { key: string; occurrences: number }[]; onMove: (key: string) => void }

// CourseCard.tsx — screen 3.
interface CourseCardProps { title: string; subtitle: string; signedIn: boolean;
  stats?: { known: number; due: number; new: number; coverage: number };   // coverage 0–100
  to: string; listTo: string }

// ChangeReviewList.tsx — screen 3 dialog. Inside the existing Dialog (size lg).
interface ChangeItem { entryId: number; lemma: string; lang: "grc" | "he"; kind: string;
  before?: string[]; after?: string[]; note?: string; removed: boolean }
interface ChangeReviewListProps { items: ChangeItem[]; onKeep: (id: number) => void; onReset: (id: number) => void }

// StudyCard.tsx — screen 4 frame: kind label (uppercase xs), children, everything else is in the children.
interface StudyCardProps { kindLabel: string; children: ReactNode }

// ChoiceList.tsx — MC, reverse MC, listening, verse context.
interface ChoiceListProps { options: string[]; lang?: "de" | "grc" | "he";
  state: { picked: number | null; correct: number | null };   // correct comes from the answer response
  onPick: (index: number) => void }   // keys 1–4 pick; correct gets border-sev-ok, wrong pick border-sev-error

// LetterBoxInput.tsx — typing card and editor preview.
interface LetterBoxInputProps { layout: TypingLayout; value: string[]; onChange: (groups: string[]) => void;
  dir?: "ltr" | "rtl"; lang: "grc" | "he"; readOnly?: boolean }
type TypingLayout = ({ group: number } | { sep: string })[];   // from GET …/next; letters never sent
// one <input maxlength=1> per letter, auto-advance, Backspace goes back; group lengths from layout

// ScriptKeyboard.tsx — port of Lexio 0049 (simple-keyboard, approve first).
interface ScriptKeyboardProps { layout: "greek" | "hebrew"; onKey: (key: string) => void }   // "⌫" = backspace
// rows exactly as in the dummy; physical keys map through the same layout

// StrengthBar.tsx — feedback and summary.
interface StrengthBarProps { before: number; after: number }   // 0–5; moved / held / dropped text from the pair

// VerseContext.tsx — intro, feedback, verse-context card, detail page.
interface VerseContextProps { reference: string; href: string; original: OriginalVerseData;
  highlight: number[]; german: string; germanHit: { start: number; end: number }; gap?: boolean }
```

## Typing layout rules (server and `LetterBoxInput` must agree)

- Groups are runs of Unicode letters with their combining marks. Everything else is a separator (comma, hyphen, space).
- A group's length is its letter count **after** removing combining marks. `ἀρχή` has 4 boxes and `בָּרָא` has 3.
- The dummy's `segment()` and `norm()` are the reference for `VocabText.Segment` and `VocabText.Normalize` (04-study-engine).

## Review checklist for the maintainer

Screens 1–7 in light and dark, at phone width and desktop. The editor's save bar names the number of learners who will be notified. The change dialog has keep/reset per word and "keep all". The typing card never asks for a comma or hyphen. MC options never contain a second correct answer (note on the reverse card).
