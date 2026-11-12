// biome-ignore-all lint/a11y/useSemanticElements: a div-based ARIA grid is required for sticky columns and virtualization (no <table>)
// biome-ignore-all lint/a11y/useFocusableInteractive: rows and headers are not focus targets; the roving tab index sits on the gridcells
// biome-ignore-all lint/a11y/useKeyWithClickEvents: keyboard handling is delegated to the grid's onKeyDown
import { useVirtualizer } from "@tanstack/react-virtual";
import {
  Fragment,
  type KeyboardEvent,
  type ReactNode,
  useCallback,
  useEffect,
  useId,
  useLayoutEffect,
  useMemo,
  useRef,
  useState,
} from "react";
import { useTranslation } from "react-i18next";
import {
  type ColumnKey,
  columnWidth,
  gridTemplate,
  navColumns,
  visibleColumns,
} from "@/lib/lemmas/columns";
import { dirtyFields, type Edits, valuesOf } from "@/lib/lemmas/edits";
import { type Cell, moveCell, type NavKey, nextEditable, typeToPick } from "@/lib/lemmas/gridNav";
import { type Labels, labelOf, titleOf } from "@/lib/lemmas/labels";
import { applicableFields, invalidFields, type Rules } from "@/lib/lemmas/rules";
import {
  type EditableField,
  type LemmaWord,
  MORPH_FIELDS,
  type MorphField,
  type WordValues,
} from "@/lib/lemmas/types";
import { Button } from "./Button";
import { Select } from "./Select";
import { TextField } from "./TextField";

export interface MorphologyGridProps {
  /** The (filtered) words to show, in reading order. */
  words: readonly LemmaWord[];
  /** Dirty rows (wordId -> values); other rows show the loaded values. */
  edits: Edits;
  labels: Labels;
  rules: Rules;
  hiddenColumns: ReadonlySet<ColumnKey>;
  /** No editing: the cursor still moves, nothing opens an editor. */
  readOnly?: boolean;
  /** Lemmas already used in the chapter, offered while typing a lemma. */
  lemmaSuggestions?: readonly string[];
  onEdit: (word: LemmaWord, field: EditableField, value: string | null) => void;
  onResetRow: (wordId: number) => void;
  /** The row under the cursor (or focused card), for the context line. */
  onActiveWordChange?: (word: LemmaWord | null) => void;
  /** Ctrl+S inside the grid. */
  onSave?: () => void;
  /**
   * Mouse-only shortcuts shown inside a column header (e.g. a bulk-edit pencil). Give them
   * `tabIndex={-1}`: the grid has a single tab stop and a tabbable header control would add one.
   */
  headerActions?: Partial<Record<ColumnKey, ReactNode>>;
}

/** Row height guess in px (h-7); rows are measured after render, so a different root font size is fine. */
const ROW_ESTIMATE = 28;
const CARD_ESTIMATE = 220;
const CARD_BREAKPOINT = 768; // 48rem
const OVERSCAN = 8;

type Move = "down" | "up" | "next" | "prev" | "stay";
interface EditorDone {
  value?: string | null;
  cancel?: boolean;
  move: Move;
}
interface Editing {
  wordId: number;
  col: ColumnKey;
  /** value to restore on Esc (dropdowns apply while editing) */
  startValue: string | null;
  /** first typed character that replaces the lemma */
  initial?: string;
}

function isMorph(key: ColumnKey): key is MorphField {
  return (MORPH_FIELDS as readonly string[]).includes(key);
}
function fieldOf(key: ColumnKey): EditableField | null {
  if (key === "lemma" || key === "partOfSpeech" || isMorph(key)) return key;
  return null;
}

function useContainerWidth(ref: React.RefObject<HTMLElement | null>): number {
  const [width, setWidth] = useState(Number.POSITIVE_INFINITY);
  useLayoutEffect(() => {
    const element = ref.current;
    if (!element || typeof ResizeObserver === "undefined") return;
    const observer = new ResizeObserver(([entry]) => {
      if (entry) setWidth(entry.contentRect.width);
    });
    observer.observe(element);
    return () => observer.disconnect();
  }, [ref]);
  return width;
}

function LemmaEditor({
  label,
  value,
  initial,
  listId,
  onDone,
}: {
  label: string;
  value: string;
  initial?: string;
  listId: string;
  onDone: (done: EditorDone) => void;
}) {
  const [draft, setDraft] = useState(initial ?? value);
  const finished = useRef(false);
  const ref = useRef<HTMLInputElement>(null);
  useEffect(() => {
    ref.current?.focus();
    if (initial === undefined) ref.current?.select();
  }, [initial]);

  const finish = (done: EditorDone) => {
    if (finished.current) return;
    finished.current = true;
    onDone(done);
  };
  const onKeyDown = (event: KeyboardEvent<HTMLInputElement>) => {
    event.stopPropagation();
    if (event.key === "Enter") {
      event.preventDefault();
      finish({ value: draft, move: event.shiftKey ? "up" : "down" });
    } else if (event.key === "Escape") {
      event.preventDefault();
      finish({ cancel: true, move: "stay" });
    } else if (event.key === "Tab") {
      event.preventDefault();
      finish({ value: draft, move: event.shiftKey ? "prev" : "next" });
    }
  };
  return (
    <TextField
      label={label}
      labelHidden
      required={false}
      size="sm"
      value={draft}
      inputRef={ref}
      list={listId}
      className="w-full"
      onChange={setDraft}
      onKeyDown={onKeyDown}
      onBlur={() => finish({ value: draft, move: "stay" })}
    />
  );
}

function SelectEditor({
  label,
  value,
  options,
  onChange,
  onDone,
  autoOpen,
}: {
  label: string;
  value: string;
  options: Labels[string];
  onChange: (value: string) => void;
  onDone: (done: EditorDone) => void;
  autoOpen: boolean;
}) {
  const ref = useRef<HTMLSelectElement>(null);
  const finished = useRef(false);
  useEffect(() => {
    ref.current?.focus();
    if (autoOpen) {
      try {
        ref.current?.showPicker?.();
      } catch {
        // showPicker needs a user gesture; the select is focused either way
      }
    }
  }, [autoOpen]);
  const finish = (done: EditorDone) => {
    if (finished.current) return;
    finished.current = true;
    onDone(done);
  };
  return (
    <Select
      label={label}
      labelHidden
      size="sm"
      value={value}
      selectRef={ref}
      className="w-full"
      options={[{ value: "", label: "–" }, ...(options ?? [])]}
      onChange={onChange}
      onBlur={() => finish({ move: "stay" })}
      onKeyDown={(event) => {
        event.stopPropagation();
        if (event.key === "Enter") {
          event.preventDefault();
          finish({ move: event.shiftKey ? "up" : "down" });
        } else if (event.key === "Escape") {
          event.preventDefault();
          finish({ cancel: true, move: "stay" });
        } else if (event.key === "Tab") {
          event.preventDefault();
          finish({ move: event.shiftKey ? "prev" : "next" });
        }
      }}
    />
  );
}

/**
 * Editable table of the German lemma and morphology of one chapter: div-based ARIA grid with sticky
 * header and left columns, virtualized rows, roving tab index and a spreadsheet-style keyboard map.
 * Below 48rem container width it renders one card per word instead (same props, same edits).
 */
export function MorphologyGrid({
  words,
  edits,
  labels,
  rules,
  hiddenColumns,
  readOnly = false,
  lemmaSuggestions = [],
  onEdit,
  onResetRow,
  onActiveWordChange,
  onSave,
  headerActions,
}: MorphologyGridProps) {
  const { t } = useTranslation();
  const listId = useId();
  const rootRef = useRef<HTMLDivElement>(null);
  const scrollRef = useRef<HTMLDivElement>(null);
  const width = useContainerWidth(rootRef);
  const cards = width < CARD_BREAKPOINT;

  const cols = useMemo(() => navColumns(hiddenColumns), [hiddenColumns]);
  const shownColumns = useMemo(() => visibleColumns(hiddenColumns), [hiddenColumns]);
  const header = useCallback((key: ColumnKey) => t(`lemmas.columns.${key}`), [t]);

  const template = useMemo(() => {
    const labelsByColumn: Partial<Record<ColumnKey, string[]>> = {};
    for (const key of shownColumns) {
      const field = fieldOf(key);
      if (field && field !== "lemma") {
        labelsByColumn[key] = (labels[field] ?? []).map((option) => option.label);
      }
    }
    const headers: Partial<Record<ColumnKey, string>> = {};
    for (const key of shownColumns) headers[key] = header(key);
    return gridTemplate(shownColumns.map((key) => columnWidth(key, labelsByColumn, headers)));
  }, [shownColumns, labels, header]);

  const [active, setActive] = useState<{ wordId: number; col: ColumnKey } | null>(null);
  const [editing, setEditing] = useState<Editing | null>(null);
  const [focusTick, setFocusTick] = useState(0);
  const [openedByPointer, setOpenedByPointer] = useState(false);

  const indexById = useMemo(() => new Map(words.map((w, i) => [w.wordId, i])), [words]);
  const activeRow =
    active && indexById.has(active.wordId) ? (indexById.get(active.wordId) as number) : 0;
  const activeCol = Math.max(0, cols.indexOf(active?.col ?? "lemma"));
  const activeWord = words[activeRow] ?? null;

  useEffect(() => {
    onActiveWordChange?.(activeWord);
  }, [activeWord, onActiveWordChange]);

  const virtualizer = useVirtualizer({
    count: words.length,
    getScrollElement: () => scrollRef.current,
    estimateSize: () => (cards ? CARD_ESTIMATE : ROW_ESTIMATE),
    overscan: OVERSCAN,
    // Size before the first measurement (and in environments without ResizeObserver, e.g. jsdom).
    initialRect: { width: 1200, height: 800 },
    rangeExtractor: (range) => {
      const indexes = new Set<number>();
      for (let i = range.startIndex - range.overscan; i <= range.endIndex + range.overscan; i++) {
        if (i >= 0 && i < range.count) indexes.add(i);
      }
      // Keep the cursor row mounted (so it keeps focus) only once the user actually has a cursor.
      if (!cards && active && words.length > 0) indexes.add(activeRow);
      return [...indexes].sort((a, b) => a - b);
    },
  });
  const items = virtualizer.getVirtualItems();
  const padTop = items[0]?.start ?? 0;
  const padBottom = items.length > 0 ? virtualizer.getTotalSize() - (items.at(-1)?.end ?? 0) : 0;
  // The kept cursor row can sit apart from the window; a spacer covers the rows between them.
  let gapIndex = -1;
  let gapHeight = 0;
  for (let i = 1; i < items.length; i++) {
    const previous = items[i - 1];
    const current = items[i];
    if (previous && current && current.index !== previous.index + 1) {
      gapIndex = current.index;
      gapHeight = current.start - previous.end;
      break;
    }
  }

  useLayoutEffect(() => {
    const element = scrollRef.current;
    if (!element) return;
    element.style.setProperty("--lemma-cols", template);
    element.style.setProperty("--lemma-pad-top", `${padTop}px`);
    element.style.setProperty("--lemma-pad-bottom", `${Math.max(0, padBottom)}px`);
    element.style.setProperty("--lemma-pad-gap", `${Math.max(0, gapHeight)}px`);
  });

  // Moves the real focus to the cursor cell after keyboard navigation.
  // biome-ignore lint/correctness/useExhaustiveDependencies: runs only on explicit focus requests
  useEffect(() => {
    if (focusTick === 0 || !active || editing) return;
    scrollRef.current
      ?.querySelector<HTMLElement>(`[data-cell="${active.wordId}:${active.col}"]`)
      ?.focus({ preventScroll: false });
  }, [focusTick]);

  const size = { rows: words.length, cols: cols.length };

  function isEditable(word: LemmaWord, key: ColumnKey): boolean {
    if (readOnly) return false;
    if (key === "lemma" || key === "partOfSpeech") return true;
    if (!isMorph(key)) return false;
    const values = editedValues(word);
    return applicableFields(rules, values.partOfSpeech, values.verbForm).has(key);
  }
  function editedValues(word: LemmaWord): WordValues {
    return edits.get(word.wordId) ?? valuesOf(word);
  }

  function goTo(cell: Cell, focus = true) {
    const word = words[cell.row];
    const col = cols[cell.col];
    if (!word || !col) return;
    setActive({ wordId: word.wordId, col });
    virtualizer.scrollToIndex(cell.row);
    if (focus) setFocusTick((tick) => tick + 1);
  }

  function startEdit(word: LemmaWord, col: ColumnKey, initial?: string, pointer = false) {
    const field = fieldOf(col);
    if (!field || !isEditable(word, col)) return;
    setActive({ wordId: word.wordId, col });
    setOpenedByPointer(pointer);
    setEditing({ wordId: word.wordId, col, startValue: editedValues(word)[field], initial });
  }

  function finishEdit(word: LemmaWord, done: EditorDone) {
    const current = editing;
    setEditing(null);
    const col = current?.col;
    const field = col ? fieldOf(col) : null;
    if (current && field) {
      if (done.cancel) {
        if (field !== "lemma") onEdit(word, field, current.startValue);
      } else if (field === "lemma" && done.value !== undefined) {
        onEdit(word, "lemma", done.value);
      }
    }
    const here = {
      row: indexById.get(word.wordId) ?? 0,
      col: Math.max(0, cols.indexOf(col ?? "lemma")),
    };
    if (done.move === "down") goTo(moveCell(here, "ArrowDown", size));
    else if (done.move === "up") goTo(moveCell(here, "ArrowUp", size));
    else if (done.move === "next" || done.move === "prev") {
      const target = nextEditable(here, done.move === "next" ? 1 : -1, size, (cell) => {
        const w = words[cell.row];
        const k = cols[cell.col];
        return !!w && !!k && isEditable(w, k);
      });
      goTo(target ?? here);
    } else goTo(here);
  }

  function onGridKeyDown(event: KeyboardEvent<HTMLDivElement>) {
    if (editing || cards) return;
    if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "s") {
      event.preventDefault();
      onSave?.();
      return;
    }
    const word = words[activeRow];
    const key = cols[activeCol];
    if (!word || !key) return;
    const here = { row: activeRow, col: activeCol };
    const navKeys: readonly string[] = [
      "ArrowUp",
      "ArrowDown",
      "ArrowLeft",
      "ArrowRight",
      "PageUp",
      "PageDown",
      "Home",
      "End",
    ];
    if (navKeys.includes(event.key)) {
      event.preventDefault();
      goTo(moveCell(here, event.key as NavKey, size, event.ctrlKey || event.metaKey));
    } else if (event.key === "Enter" || event.key === "F2") {
      event.preventDefault();
      startEdit(word, key);
    } else if (event.key === "Tab") {
      const target = nextEditable(here, event.shiftKey ? -1 : 1, size, (cell) => {
        const w = words[cell.row];
        const k = cols[cell.col];
        return !!w && !!k && isEditable(w, k);
      });
      if (target) {
        event.preventDefault();
        goTo(target);
      }
    } else if (event.key === "Delete" || event.key === "Backspace") {
      const field = fieldOf(key);
      if (
        field &&
        (isEditable(word, key) ||
          invalidFields(rules, editedValues(word)).includes(field as MorphField)) &&
        !readOnly
      ) {
        event.preventDefault();
        onEdit(word, field, null);
      }
    } else if (event.key.length === 1 && !event.ctrlKey && !event.metaKey && !event.altKey) {
      if (!isEditable(word, key)) return;
      event.preventDefault();
      if (key === "lemma") startEdit(word, key, event.key);
      else {
        const field = fieldOf(key) as EditableField;
        const picked = typeToPick(labels[field] ?? [], editedValues(word)[field], event.key);
        if (picked !== null) onEdit(word, field, picked);
      }
    }
  }

  function renderCell(word: LemmaWord, key: ColumnKey, rowIndex: number): ReactNode {
    const values = editedValues(word);
    const base = valuesOf(word);
    const field = fieldOf(key);
    const isActive = active?.wordId === word.wordId && active.col === key;
    const colIndex = cols.indexOf(key);
    const dirty = field ? values[field] !== base[field] : false;
    const allowed = applicableFields(rules, values.partOfSpeech, values.verbForm);
    const pos = labelOf(labels, "partOfSpeech", values.partOfSpeech) || t("lemmas.grid.noPos");
    const common = {
      role: "gridcell" as const,
      "aria-colindex": colIndex + 2,
      "data-cell": `${word.wordId}:${key}`,
      tabIndex: isActive ? 0 : -1,
      "aria-selected": isActive || undefined,
      onFocus: () => {
        if (!isActive) setActive({ wordId: word.wordId, col: key });
      },
    };
    const cellClass = `flex h-7 items-center truncate border-b border-border px-1.5 -outline-offset-2 focus-visible:outline-2 ${
      dirty ? "bg-mark-linked" : "bg-surface"
    } ${isActive ? "bg-surface-muted" : ""}`;

    if (editing && editing.wordId === word.wordId && editing.col === key && field) {
      const label = t("lemmas.grid.editor", { field: header(key), word: word.wordInContext });
      return (
        <div key={key} {...common} className={cellClass}>
          {field === "lemma" ? (
            <LemmaEditor
              label={label}
              value={values.lemma ?? ""}
              initial={editing.initial}
              listId={listId}
              onDone={(done) => finishEdit(word, done)}
            />
          ) : (
            <SelectEditor
              label={label}
              value={values[field] ?? ""}
              options={labels[field] ?? []}
              autoOpen={openedByPointer}
              onChange={(value) => onEdit(word, field, value || null)}
              onDone={(done) => finishEdit(word, done)}
            />
          )}
        </div>
      );
    }

    const onPointer = () => {
      if (!field) return;
      if (field !== "lemma") startEdit(word, key, undefined, true);
      else if (isActive) startEdit(word, key);
    };
    const select = () => {
      setActive({ wordId: word.wordId, col: key });
    };

    let content: ReactNode;
    let title: string | undefined;
    if (key === "context") {
      content = <span className="truncate font-scripture">{word.wordInContext}</span>;
    } else if (key === "plain") {
      content = <span className="truncate text-fg-muted">{word.plainWord ?? ""}</span>;
    } else if (field) {
      const value = values[field];
      const invalid = isMorph(key) && !allowed.has(key) && value !== null;
      const notApplicable = isMorph(key) && !allowed.has(key) && value === null;
      if (invalid) {
        content = (
          <span className="truncate">
            <span aria-hidden="true">⚠ </span>
            <s>{labelOf(labels, key, value)}</s>
            <span className="sr-only">{t("lemmas.grid.invalidShort")}</span>
          </span>
        );
        title = t("lemmas.grid.invalid", {
          field: header(key),
          value: labelOf(labels, key, value),
          pos,
        });
      } else if (notApplicable) {
        content = (
          <span className="text-fg-muted" aria-hidden="true">
            –
          </span>
        );
        title = t("lemmas.grid.notApplicable", { pos });
      } else if (!value && (key === "lemma" || key === "partOfSpeech")) {
        content = (
          <span className="italic text-fg-muted underline decoration-dashed">
            {t("lemmas.grid.missing")}
          </span>
        );
      } else {
        content = (
          <span className="truncate">{key === "lemma" ? value : labelOf(labels, key, value)}</span>
        );
        title = key === "lemma" ? undefined : titleOf(labels, key, value) || undefined;
      }
      if (dirty) {
        const before = base[field];
        title = `${title ? `${title}\n` : ""}${t("lemmas.grid.before", {
          value: before ? (key === "lemma" ? before : labelOf(labels, key, before)) : "–",
        })}`;
      }
    }
    return (
      // biome-ignore lint/a11y/noStaticElementInteractions: grid cells are driven by the grid's keyboard map
      <div
        key={key}
        {...common}
        aria-disabled={field && isMorph(key) && !allowed.has(key) ? true : undefined}
        title={title}
        className={`${cellClass} ${key === "context" ? "sticky left-12 z-10 border-r" : ""}`}
        onMouseDown={select}
        onClick={onPointer}
        onDoubleClick={() => key === "lemma" && startEdit(word, key)}
        data-row={rowIndex}
      >
        {content}
        {dirty && <span className="sr-only">, {t("lemmas.grid.changed")}</span>}
      </div>
    );
  }

  const datalist = (
    <datalist id={listId}>
      {lemmaSuggestions.map((lemma) => (
        <option key={lemma} value={lemma} />
      ))}
    </datalist>
  );

  if (cards) {
    return (
      <div ref={rootRef} className="flex min-h-0 flex-1 flex-col">
        {datalist}
        <div ref={scrollRef} className="lemma-cards-height overflow-y-auto">
          <ul className="flex flex-col gap-2" aria-label={t("lemmas.grid.label")}>
            <li aria-hidden="true" className="lemma-pad-top" />
            {items.map((item) => {
              const word = words[item.index] as LemmaWord;
              return (
                <li key={word.wordId} data-index={item.index} ref={virtualizer.measureElement}>
                  <WordCard
                    word={word}
                    previous={words[item.index - 1]}
                    values={editedValues(word)}
                    base={valuesOf(word)}
                    labels={labels}
                    rules={rules}
                    readOnly={readOnly}
                    listId={listId}
                    onEdit={(field, value) => onEdit(word, field, value)}
                    onReset={() => onResetRow(word.wordId)}
                    onFocus={() => onActiveWordChange?.(word)}
                  />
                </li>
              );
            })}
            <li aria-hidden="true" className="lemma-pad-bottom" />
          </ul>
        </div>
      </div>
    );
  }

  return (
    <div ref={rootRef} className="flex min-h-0 flex-1 flex-col">
      {datalist}
      <div
        ref={scrollRef}
        role="grid"
        aria-label={t("lemmas.grid.label")}
        aria-rowcount={words.length + 1}
        aria-colcount={shownColumns.length}
        aria-readonly={readOnly || undefined}
        onKeyDown={onGridKeyDown}
        className="relative min-h-0 flex-1 overflow-auto rounded-md border border-border text-sm"
      >
        <div
          role="row"
          aria-rowindex={1}
          className="lemma-row sticky top-0 z-20 bg-surface-muted font-medium"
        >
          {shownColumns.map((key, i) => (
            <div
              key={key}
              role="columnheader"
              aria-colindex={i + 1}
              className={`truncate border-b border-border px-1.5 py-1 ${
                key === "verse" ? "sticky left-0 z-30 bg-surface-muted" : ""
              } ${key === "context" ? "sticky left-12 z-30 border-r bg-surface-muted" : ""}`}
            >
              {headerActions?.[key] ? (
                <span className="flex items-center gap-1">
                  {header(key)}
                  {headerActions[key]}
                </span>
              ) : (
                header(key)
              )}
            </div>
          ))}
        </div>
        <div aria-hidden="true" className="lemma-pad-top" />
        {items.map((item) => {
          const word = words[item.index] as LemmaWord;
          const previous = words[item.index - 1];
          const firstOfVerse = previous?.verse !== word.verse;
          const base = valuesOf(word);
          const rowDirty = dirtyFields(base, editedValues(word)).length > 0;
          return (
            <Fragment key={word.wordId}>
              {item.index === gapIndex && <div aria-hidden="true" className="lemma-pad-gap" />}
              <div
                role="row"
                aria-rowindex={item.index + 2}
                data-index={item.index}
                ref={virtualizer.measureElement}
                className={`lemma-row ${firstOfVerse && item.index > 0 ? "border-t-2 border-border" : ""}`}
              >
                {shownColumns.map((key, i) => {
                  if (key !== "verse") return renderCell(word, key, item.index);
                  return (
                    <div
                      key={key}
                      role="rowheader"
                      aria-colindex={i + 1}
                      className={`sticky left-0 z-10 flex h-7 items-center gap-0.5 border-b border-border bg-surface px-1 text-fg-muted ${
                        rowDirty ? "border-l-4 border-l-accent" : ""
                      }`}
                    >
                      {firstOfVerse ? (
                        <span className="font-medium text-fg">{word.verse}</span>
                      ) : null}
                      {rowDirty && (
                        <>
                          <span className="sr-only">{t("lemmas.grid.changed")}</span>
                          <Button
                            variant="ghost"
                            size="sm"
                            className="ml-auto px-1 py-0"
                            aria-label={t("lemmas.grid.resetRow")}
                            title={t("lemmas.grid.resetRow")}
                            onClick={() => onResetRow(word.wordId)}
                          >
                            ↺
                          </Button>
                        </>
                      )}
                    </div>
                  );
                })}
              </div>
            </Fragment>
          );
        })}
        <div aria-hidden="true" className="lemma-pad-bottom" />
      </div>
    </div>
  );
}

function WordCard({
  word,
  previous,
  values,
  base,
  labels,
  rules,
  readOnly,
  listId,
  onEdit,
  onReset,
  onFocus,
}: {
  word: LemmaWord;
  previous: LemmaWord | undefined;
  values: WordValues;
  base: WordValues;
  labels: Labels;
  rules: Rules;
  readOnly: boolean;
  listId: string;
  onEdit: (field: EditableField, value: string | null) => void;
  onReset: () => void;
  onFocus: () => void;
}) {
  const { t } = useTranslation();
  const allowed = applicableFields(rules, values.partOfSpeech, values.verbForm);
  const invalid = invalidFields(rules, values);
  const dirty = dirtyFields(base, values).length > 0;
  const pos = labelOf(labels, "partOfSpeech", values.partOfSpeech) || t("lemmas.grid.noPos");
  const fields = MORPH_FIELDS.filter((field) => allowed.has(field));
  return (
    // biome-ignore lint/a11y/noStaticElementInteractions: focus bubbling only feeds the context line
    <div onFocus={onFocus} className="flex flex-col gap-2">
      {previous?.verse !== word.verse && (
        <h3 className="sticky top-0 z-10 bg-surface-muted px-2 py-1 text-sm font-semibold">
          {t("lemmas.context", { verse: word.verse })}
        </h3>
      )}
      <article
        className={`flex flex-col gap-2 rounded-md border p-3 ${
          dirty ? "border-accent bg-mark-linked" : "border-border bg-surface"
        }`}
      >
        <header className="flex items-center gap-2">
          <span className="font-scripture text-base font-semibold">{word.wordInContext}</span>
          {dirty && (
            <>
              <span className="rounded-sm bg-surface-muted px-1 text-xs">
                {t("lemmas.grid.changed")}
              </span>
              <Button
                variant="ghost"
                size="sm"
                className="ml-auto"
                aria-label={t("lemmas.grid.resetRow")}
                onClick={onReset}
              >
                ↺
              </Button>
            </>
          )}
        </header>
        <TextField
          label={t("lemmas.columns.lemma")}
          required={false}
          value={values.lemma ?? ""}
          list={listId}
          disabled={readOnly}
          invalid={!values.lemma}
          onChange={(value) => onEdit("lemma", value || null)}
        />
        <Select
          label={t("lemmas.columns.partOfSpeech")}
          size="md"
          value={values.partOfSpeech ?? ""}
          disabled={readOnly}
          invalid={!values.partOfSpeech}
          options={[{ value: "", label: "–" }, ...(labels.partOfSpeech ?? [])]}
          onChange={(value) => onEdit("partOfSpeech", value || null)}
        />
        {fields.length > 0 && (
          <div className="grid grid-cols-2 gap-2">
            {fields.map((field) => (
              <Select
                key={field}
                label={t(`lemmas.columns.${field}`)}
                size="md"
                value={values[field] ?? ""}
                disabled={readOnly}
                className="flex-col items-start"
                options={[{ value: "", label: "–" }, ...(labels[field] ?? [])]}
                onChange={(value) => onEdit(field, value || null)}
              />
            ))}
          </div>
        )}
        {invalid.map((field) => (
          <p key={field} className="flex items-center gap-2 text-sm text-accent">
            <span>
              ⚠{" "}
              {t("lemmas.grid.invalid", {
                field: t(`lemmas.columns.${field}`),
                value: labelOf(labels, field, values[field]),
                pos,
              })}
            </span>
            {!readOnly && (
              <Button size="sm" onClick={() => onEdit(field, null)}>
                {t("lemmas.grid.clear")}
              </Button>
            )}
          </p>
        ))}
      </article>
    </div>
  );
}
