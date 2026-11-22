# 0023 · Scripture references in notes

## German scan mode

`ScriptureScanner` only finds OSIS candidates (`Book.digit`). Notes are written in German citation style, so the scanner gets a second options set `GermanTextOptions` (`AliasProfile.German`, `WholeInput: false`, Hebrew counting as `api/references/parse`) with its own candidate regex: a German book alias (from `BookAliasCatalog`, incl. `1. Mose`, `1Mo`, `Röm`, `Offb`, full names) followed by optional space and a chapter digit. `ScriptureReferenceParser.ParseAt` decides the extent as today.

False-positive guard (German prose is full of short words that are book aliases):

- Aliases that are ordinary German words (at least `Am`, `Ri`, `Hi`, `Ob`, `Mi`; the full list is drawn from the alias catalogue in row 0023.3) count only with `chapter,verse` (`Am 3,7`), never with a chapter alone (`am 3. Tag` stays text). The list lives in `BookAliasCatalog` as `RequiresVerseInProse` and is covered by tests.
- Case-sensitive book match in prose (`am` never matches `Am`).
- A chapter number followed by `.` and a non-digit (`am 3. Tag`) is no reference.

## Where links are made

`NoteBodyLinker` (Application, pure): input Markdown, output Markdown in which every found reference becomes a Markdown link `[Röm 8,28](/bible/45/8?v=28#v28)`. It tokenizes just enough Markdown to skip fenced code blocks, inline code and the text and target of existing links/autolinks. Path via `ReaderPath`, several groups (`Röm 8,28; 12,1-2`) become one link per group, the visible text stays exactly as typed.

The API returns both `bodyMarkdown` (raw, for the editor) and `bodyLinked` (for display). Computed on every read; a chapter rarely has more than a handful of notes, the list page pages at 50.

## Display

The frontend renders `bodyLinked` with MDXEditor in `readOnly` mode (same plugin set as the editor, so what you write is what you see). Links to `/bible/...` are intercepted and routed by the SPA (no full reload) and get the `PassagePreviewCard` hover preview like `ScriptureText`. External `http(s)` links open in a new tab with `rel="noopener noreferrer"`; other schemes (`javascript:` etc.) are not rendered as links.

If MDXEditor read-only turns out too heavy for the list page (many instances), the fallback is one editor instance per expanded note and a plain-text excerpt for collapsed rows — decided in row 0023.4f, not before.
