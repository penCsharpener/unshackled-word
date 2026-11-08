# Components

Registry of the shared UI components of the React frontend (`src/UnshackledWord.Web/src/components`). Agents: check this list before building anything and reuse or extend an entry (add a prop or variant) instead of creating a new component. Every new component is proposed in chat first, then added here with a one-line description. `bun run check:components` fails if a component file is missing from this table or if two components share a name.

Rules: design tokens only (Tailwind theme variables from `src/styles/index.css`), no hardcoded colors, spacing or font sizes, no raw `<button>`, no inline styles, no arbitrary Tailwind values.

| Component | Description |
| --- | --- |
| `Button` | The only button. Variants `primary` / `secondary` / `ghost`, sizes `sm` / `md`; defaults to `type="button"`. |
| `ThemeToggle` | System / light / dark switch, persisted in localStorage. |
| `LanguageSwitch` | DE / EN switch (i18next), persisted in localStorage. |
| `AppShell` | Page frame: header with brand, language and theme controls, plus the routed `<Outlet />`. |
| `BibleChapterPicker` | OT/NT bar that unrolls a book grid with chapter links (Psalms split in five books); props `books`, `currentBookId`, `currentChapter`, `defaultTestament`. |
