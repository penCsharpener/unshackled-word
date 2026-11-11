# Components

Registry of the shared UI components of the React frontend (`src/UnshackledWord.Web/src/components`). Agents: check this list before building anything and reuse or extend an entry (add a prop or variant) instead of creating a new component. Every new component is proposed in chat first, then added here with a one-line description. `bun run check:components` fails if a component file is missing from this table or if two components share a name.

Rules: design tokens only (Tailwind theme variables from `src/styles/index.css`), no hardcoded colors, spacing or font sizes, no raw `<button>`, no inline styles, no arbitrary Tailwind values.

| Component | Description |
| --- | --- |
| `Button` | The only button. Variants `primary` / `secondary` / `ghost` / `inline` (text flow, inherits font), sizes `sm` / `md`; defaults to `type="button"`. |
| `ThemeToggle` | One icon button cycling system / light / dark, persisted in localStorage. |
| `LanguageSwitch` | DE / EN switch (i18next), persisted in localStorage. |
| `AppShell` | Page frame: header with brand, language and theme controls, plus the routed `<Outlet />`. |
| `BibleChapterPicker` | OT/NT bar that unrolls a book grid with chapter links (Psalms split in five books); props `books`, `currentBookId`, `currentChapter`, `defaultTestament`. |
| `WordDetailPopover` | Pop-up with the Greek/Hebrew readings of one Elberfelder word (native popover; anchored on desktop, bottom sheet on mobile); shows TSK cross references of the verse (collapsible, linked), mapping provenance badge (AI suggestion / confirmed / manual), words translated together, uncertainty hints and an edit-mapping action (disabled until `onEditMapping` is passed); props `word`, `status`, `detail`, `books`, `isSample`, `onRetry`, `onClose`, `onEditMapping`. |
| `MappingEditor` | Inline editor below a verse to correct the German - original word mapping (drag and drop or select-then-tap, group badges, undo, diff summary, Save/Cancel; read-only when editing is disabled); props `data`, `reference`, `saving`, `saveError`, `onSave`, `onCancel`. |
| `HeaderWordInfo` | Centre of the sticky top menu: original word, lemma, Strong's and decoded morphology of the hovered word (fed through `lib/headerInfo`). |
| `OriginalVerse` | Plain original-language text of one verse (no transliteration) with optional Strong's / morphology per word; props `data`, `reference`, `showLemma`, `showStrongs`, `showMorphology`, `onToggleLemma`, `onToggleStrongs`, `onToggleMorphology`. |
| `MappingToken` | One word tile of the mapping editor (German or original) with group badge, status, selection and drag/drop handlers; built on `Button`. |
| `Select` | Labelled native select styled with tokens; props `label`, `value`, `onChange(value)`, `options` (`{ value, label, disabled? }`), `disabled`, `size` (`sm` / `md`). |
| `EmptyState` | Dashed placeholder box; props `title`, `children`, `variant` (`empty` / `comingSoon` adds hidden skeleton rows), `skeletonRows`. |
| `BookBarChart` | Occurrences per Bible book as SVG columns (list view on narrow containers, list/chart toggle from 40rem), bars coloured by the 8 Bible sections with legend, zeros muted, text alternative per book; props `books`, `counts`, `testaments`, `title`, `summary`, `selectedBookId`, `onSelectBook(bookId \| null)`, `defaultView`. |
| `DonutChart` | Ring chart with legend (count and percent per slice), screen-reader table, optional filter `Select`; more than 8 slices fold into "Other"; props `title`, `subtitle`, `note`, `slices` (`{ key, label, text?, value, highlight? }`), `centerValue`, `centerLabel`, `activeKey`, `onSelectSlice(key \| null)`, `filterLabel`. |
| `StrongsStatsHeader` | Top of the Strong's page: heading with lemma and glosses, stat line, "formed from" placeholder, occurrences per book (`BookBarChart`) and translation donuts (`DonutChart`: renderings, plus the Strong's numbers behind the selected rendering in OT and NT); loads its own data (`lib/bible/strongsStats`), shows loading/error with retry/empty states; props `strongs`, `bookId`, `rendering`, `onSelectBook`, `onSelectRendering` (bar and slice clicks drive the concordance filters). |
