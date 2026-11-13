import { useQueryClient } from "@tanstack/react-query";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { Link, useBlocker, useParams, useSearchParams } from "react-router";
import { BibleChapterPicker } from "@/components/BibleChapterPicker";
import { Button, buttonClasses } from "@/components/Button";
import { EmptyState } from "@/components/EmptyState";
import { LemmaBulkEdit } from "@/components/LemmaBulkEdit";
import { MorphologyGrid } from "@/components/MorphologyGrid";
import { SaveBar, type SaveStatus } from "@/components/SaveBar";
import { Select } from "@/components/Select";
import { TextField } from "@/components/TextField";
import { ToggleMenu } from "@/components/ToggleMenu";
import { ApiError, describeApiError } from "@/lib/api";
import { useAuth } from "@/lib/auth/AuthContext";
import { bookName, parseChapterParams } from "@/lib/bible/books";
import { useBibleStructure } from "@/lib/bible/queries";
import type { BibleStructureBook, ChapterRef } from "@/lib/bible/types";
import { lemmaApi } from "@/lib/lemmas/api";
import { type ColumnKey, HIDEABLE_COLUMNS } from "@/lib/lemmas/columns";
import {
  applyEdit,
  buildChanges,
  chunk,
  dropEdits,
  type Edits,
  resetRow,
  valuesOf,
} from "@/lib/lemmas/edits";
import {
  type ChapterStats,
  chapterStats,
  currentValues,
  type Filters,
  filterWords,
  NO_FILTERS,
} from "@/lib/lemmas/filter";
import { buildLabels } from "@/lib/lemmas/labels";
import { lemmaKeys, useLemmaChapter, useLemmaValueMaps } from "@/lib/lemmas/queries";
import { buildRules } from "@/lib/lemmas/rules";
import { buildStts } from "@/lib/lemmas/stts";
import type { EditableField, LemmaChapterResponse, LemmaWord } from "@/lib/lemmas/types";
import { useBulkUrl } from "@/lib/lemmas/useBulkUrl";

const COLUMNS_KEY = "uw.lemmaEditor.hiddenColumns";
const MAX_CHANGES_PER_REQUEST = 2000;
const WIDE_SCREEN_PX = 1440; // 90rem: the cleaned word column is shown by default from here
const NO_EDITS: Edits = new Map();

export const lemmaEditorPath = (ref: ChapterRef) => `/edit/lemmas/${ref.bookId}/${ref.chapter}`;

function loadHiddenColumns(): Set<ColumnKey> {
  try {
    const stored = localStorage.getItem(COLUMNS_KEY);
    if (stored) return new Set(JSON.parse(stored) as ColumnKey[]);
  } catch {
    // storage unavailable: use the default
  }
  return new Set(window.innerWidth < WIDE_SCREEN_PX ? (["plain"] as ColumnKey[]) : []);
}

export function LemmaEditorPage() {
  const { t, i18n } = useTranslation();
  const params = useParams();
  const { status, user } = useAuth();
  const structure = useBibleStructure();
  const books = structure.data;
  const ref = parseChapterParams(params.bookId, params.chapter, books);

  const header = (
    <>
      <h1 className="font-scripture text-2xl font-semibold">{t("lemmas.title")}</h1>
      <BibleChapterPicker
        books={books}
        currentBookId={ref?.bookId}
        currentChapter={ref?.chapter}
        chapterHref={lemmaEditorPath}
      />
    </>
  );

  if (status === "loading") return <p className="text-fg-muted">{t("auth.loading")}</p>;
  if (status !== "authenticated") {
    return (
      <div className="flex flex-col gap-4">
        <h1 className="font-scripture text-2xl font-semibold">{t("lemmas.title")}</h1>
        <EmptyState title={t("lemmas.loginRequired.title")}>
          <p className="mb-3">{t("lemmas.loginRequired.text")}</p>
          <Link to="/login" className={buttonClasses({ variant: "primary" })}>
            {t("auth.signIn")}
          </Link>
        </EmptyState>
      </div>
    );
  }

  const book = books?.find((b) => b.id === ref?.bookId);
  const canEdit = !!user?.roles?.some((role) =>
    ["admin", "maintainer"].includes(role.toLowerCase()),
  );

  return (
    <div className="flex min-h-0 flex-1 flex-col gap-3">
      {header}
      {ref && book ? (
        <ChapterEditor
          key={`${ref.bookId}:${ref.chapter}`}
          bookId={ref.bookId}
          chapter={ref.chapter}
          title={`${bookName(book, i18n.language)} ${ref.chapter}`}
          canEdit={canEdit}
          books={books ?? []}
        />
      ) : (
        <EmptyState title={t("lemmas.noChapter.title")}>{t("lemmas.noChapter.text")}</EmptyState>
      )}
    </div>
  );
}

function saveErrorMessage(error: unknown, t: (key: string) => string): string {
  if (error instanceof ApiError) {
    if (error.status === 401) return t("lemmas.save.sessionExpired");
    if (error.status === 403) return t("lemmas.save.forbidden");
    if (error.status === 400 || error.status === 404) {
      return describeApiError(error, t("lemmas.save.error"));
    }
  }
  return t("lemmas.save.error");
}

function ChapterEditor({
  bookId,
  chapter,
  title,
  canEdit,
  books,
}: {
  bookId: number;
  chapter: number;
  title: string;
  canEdit: boolean;
  books: readonly BibleStructureBook[];
}) {
  const [searchParams] = useSearchParams();
  // `?word=<elbWordId>` (data-quality jump) opens the grid on that word
  const wordParam = searchParams.get("word");
  const focusWordId = wordParam && /^\d+$/.test(wordParam) ? Number(wordParam) : undefined;
  const { t, i18n } = useTranslation();
  const queryClient = useQueryClient();
  const maps = useLemmaValueMaps(true);
  const data = useLemmaChapter(bookId, chapter);

  const labels = useMemo(() => buildLabels(maps.data, i18n.language), [maps.data, i18n.language]);
  const rules = useMemo(() => buildRules(maps.data?.rules), [maps.data]);
  const stts = useMemo(() => buildStts(maps.data, labels), [maps.data, labels]);

  const [edits, setEdits] = useState<Edits>(NO_EDITS);
  // Filters are applied to the edits as they were when the filter last changed, so a row that stops
  // matching because of an edit stays in the list until the filter changes.
  const [filterState, setFilterState] = useState<{ filters: Filters; snapshot: Edits }>({
    filters: NO_FILTERS,
    snapshot: NO_EDITS,
  });
  const [hidden, setHidden] = useState<Set<ColumnKey>>(loadHiddenColumns);
  const [saveStatus, setSaveStatus] = useState<SaveStatus>("idle");
  const [saveMessage, setSaveMessage] = useState("");
  const [savedCount, setSavedCount] = useState(0);
  const [activeWord, setActiveWord] = useState<LemmaWord | null>(null);
  const [showKeys, setShowKeys] = useState(false);

  const words = data.data?.words;
  const wordsById = useMemo(() => new Map((words ?? []).map((w) => [w.wordId, w])), [words]);
  const visible = useMemo(
    () => (words ? filterWords(words, filterState.snapshot, filterState.filters) : []),
    [words, filterState],
  );
  const stats: ChapterStats = useMemo(
    () => chapterStats(words ?? [], edits, rules),
    [words, edits, rules],
  );
  const suggestions = useMemo(
    () => [...new Set((words ?? []).map((w) => w.lemma).filter((l): l is string => !!l))].sort(),
    [words],
  );
  const bases = useMemo(
    () => new Map((words ?? []).map((w) => [w.wordId, valuesOf(w)] as const)),
    [words],
  );
  const basesRef = useRef(bases);
  basesRef.current = bases;
  const built = useMemo(() => buildChanges(edits, bases), [edits, bases]);
  const dirty = edits.size > 0;

  // Only leaving the page is blocked; the bulk dialog and its paging change just the search params.
  const bulkUrl = useBulkUrl();
  const activeLemma = activeWord ? currentValues(activeWord, edits).lemma : null;
  const openBulk = () => bulkUrl.go({ step: "search" });
  const blocker = useBlocker(
    ({ currentLocation, nextLocation }) =>
      dirty && currentLocation.pathname !== nextLocation.pathname,
  );
  useEffect(() => {
    if (blocker.state !== "blocked") return;
    if (window.confirm(t("lemmas.save.leave"))) blocker.proceed();
    else blocker.reset();
  }, [blocker, t]);
  useEffect(() => {
    if (!dirty) return;
    const handler = (event: BeforeUnloadEvent) => event.preventDefault();
    window.addEventListener("beforeunload", handler);
    return () => window.removeEventListener("beforeunload", handler);
  }, [dirty]);

  const onEdit = useCallback(
    (word: LemmaWord, field: EditableField, value: string | null) => {
      if (!canEdit) return;
      setEdits((current) => applyEdit(current, word, rules, field, value));
      setSaveStatus((s) => (s === "saved" || s === "error" ? "idle" : s));
    },
    [canEdit, rules],
  );
  const onResetRow = useCallback((wordId: number) => setEdits((e) => resetRow(e, wordId)), []);

  function updateFilters(patch: Partial<Filters>) {
    setFilterState((state) => ({ filters: { ...state.filters, ...patch }, snapshot: edits }));
  }

  const editsRef = useRef(edits);
  editsRef.current = edits;
  const savingRef = useRef(false);

  const save = useCallback(async () => {
    const { changes, incompleteWordIds } = buildChanges(editsRef.current, basesRef.current);
    if (changes.length === 0 && incompleteWordIds.length === 0) return true;
    if (savingRef.current || !canEdit || incompleteWordIds.length > 0) return false;
    savingRef.current = true;
    setSaveStatus("saving");
    setSaveMessage("");
    let saved = 0;
    try {
      for (const part of chunk(changes, MAX_CHANGES_PER_REQUEST)) {
        const response = await lemmaApi.saveChapter(part);
        const rows = new Map(response.words.map((w) => [w.wordId, w]));
        queryClient.setQueryData<LemmaChapterResponse>(lemmaKeys.chapter(bookId, chapter), (old) =>
          old ? { ...old, words: old.words.map((w) => rows.get(w.wordId) ?? w) } : old,
        );
        setEdits((current) =>
          dropEdits(
            current,
            part.map((c) => c.wordId),
          ),
        );
        saved += part.length;
      }
      setSavedCount(saved);
      setSaveStatus("saved");
      return true;
    } catch (error) {
      setSavedCount(saved);
      setSaveMessage(saveErrorMessage(error, t));
      setSaveStatus("error");
      return false;
    } finally {
      savingRef.current = false;
    }
  }, [bookId, chapter, canEdit, queryClient, t]);

  useEffect(() => {
    if (saveStatus !== "saved") return;
    const timer = window.setTimeout(() => setSaveStatus("idle"), 4000);
    return () => window.clearTimeout(timer);
  }, [saveStatus]);

  const toggleColumn = (key: string, checked: boolean) => {
    setHidden((current) => {
      const next = new Set(current);
      if (checked) next.delete(key as ColumnKey);
      else next.add(key as ColumnKey);
      try {
        localStorage.setItem(COLUMNS_KEY, JSON.stringify([...next]));
      } catch {
        // not persisted
      }
      return next;
    });
  };
  const resetColumns = () => {
    try {
      localStorage.removeItem(COLUMNS_KEY);
    } catch {
      // nothing stored
    }
    setHidden(loadHiddenColumns());
  };

  if (data.isError || maps.isError) {
    return (
      <EmptyState title={t("lemmas.loadError")}>
        <Button
          onClick={() => {
            void data.refetch();
            void maps.refetch();
          }}
        >
          {t("lemmas.retry")}
        </Button>
      </EmptyState>
    );
  }
  if (!words || !maps.data) {
    return (
      <p aria-busy="true" className="text-fg-muted">
        {t("lemmas.loading")}
      </p>
    );
  }

  const posOptions = [
    { value: "", label: t("lemmas.filter.posAll") },
    { value: "none", label: t("lemmas.filter.posNone") },
    ...(labels.partOfSpeech ?? []),
  ];
  const { filters } = filterState;
  const verseWords = activeWord ? words.filter((w) => w.verse === activeWord.verse) : [];
  const incomplete = built.incompleteWordIds.length;
  const columnItems = HIDEABLE_COLUMNS.map((key) => ({
    key,
    label: t(`lemmas.columns.${key}`),
    checked: !hidden.has(key),
  }));

  return (
    <>
      <div>
        <p className="text-sm text-fg-muted">
          {title} · {t("lemmas.words", { count: stats.total })} ·{" "}
          {t("lemmas.missingLemma", { count: stats.missingLemma })} ·{" "}
          {t("lemmas.issues", { count: stats.inconsistencies })}
        </p>
        <p className="text-sm text-fg-muted">
          {canEdit ? t("lemmas.hintEdit") : t("lemmas.hintReadOnly")}
        </p>
      </div>
      <div className="flex flex-wrap items-end gap-x-3 gap-y-2">
        <TextField
          label={t("lemmas.filter.search")}
          size="sm"
          required={false}
          type="search"
          value={filters.search}
          onChange={(search) => updateFilters({ search })}
        />
        <Select
          label={t("lemmas.filter.pos")}
          value={filters.partOfSpeech}
          options={posOptions}
          onChange={(partOfSpeech) => updateFilters({ partOfSpeech })}
        />
        <Button
          size="sm"
          aria-pressed={filters.onlyMissingLemma}
          onClick={() => updateFilters({ onlyMissingLemma: !filters.onlyMissingLemma })}
        >
          {t("lemmas.filter.onlyMissing", { count: stats.missingLemma })}
        </Button>
        {canEdit && (
          <Button
            size="sm"
            aria-haspopup="dialog"
            title={t("lemmas.bulk.openTitle")}
            onClick={openBulk}
          >
            {t("lemmas.bulk.open")}
          </Button>
        )}
        <ToggleMenu
          className="max-md:hidden"
          label={t("lemmas.columns.menu", {
            shown: HIDEABLE_COLUMNS.length - hidden.size + 4,
            total: HIDEABLE_COLUMNS.length + 4,
          })}
          items={columnItems}
          onChange={toggleColumn}
          onReset={resetColumns}
          resetLabel={t("lemmas.columns.reset")}
        />
        <Button
          size="sm"
          className="max-md:hidden"
          aria-expanded={showKeys}
          onClick={() => setShowKeys((value) => !value)}
        >
          {t("lemmas.keyboard.button")}
        </Button>
        <span className="text-sm text-fg-muted">
          {t("lemmas.filter.shown", { shown: visible.length, total: words.length })}
        </span>
      </div>
      {showKeys && (
        <ul className="list-disc rounded-md border border-border bg-surface px-6 py-2 text-sm max-md:hidden">
          {(["move", "edit", "tab", "type", "clear", "escape", "jump", "save"] as const).map(
            (k) => (
              <li key={k}>{t(`lemmas.keyboard.${k}`)}</li>
            ),
          )}
        </ul>
      )}
      <p className="min-h-6 font-scripture text-base max-md:hidden">
        {verseWords.map((word) => (
          <span key={word.wordId}>
            {word.wordId === activeWord?.wordId ? (
              <mark className="rounded-sm bg-mark-same text-fg">{word.wordInContext}</mark>
            ) : (
              word.wordInContext
            )}{" "}
          </span>
        ))}
      </p>
      {visible.length === 0 ? (
        <EmptyState title={t("lemmas.filter.noMatch")}>
          <Button onClick={() => updateFilters({ ...NO_FILTERS })}>
            {t("lemmas.filter.reset")}
          </Button>
        </EmptyState>
      ) : (
        <MorphologyGrid
          words={visible}
          edits={edits}
          labels={labels}
          rules={rules}
          stts={stts}
          hiddenColumns={hidden}
          readOnly={!canEdit}
          lemmaSuggestions={suggestions}
          onEdit={onEdit}
          onResetRow={onResetRow}
          onActiveWordChange={setActiveWord}
          focusWordId={focusWordId}
          onSave={() => void save()}
          headerActions={
            canEdit
              ? {
                  lemma: (
                    <Button
                      variant="ghost"
                      size="sm"
                      tabIndex={-1}
                      className="px-1 py-0"
                      aria-haspopup="dialog"
                      aria-label={t("lemmas.bulk.open")}
                      title={t("lemmas.bulk.openTitle")}
                      onClick={openBulk}
                    >
                      <span aria-hidden="true">✎</span>
                    </Button>
                  ),
                }
              : undefined
          }
        />
      )}
      {canEdit && (
        <LemmaBulkEdit
          initialFrom={activeLemma}
          bookId={bookId}
          chapter={chapter}
          books={books}
          partOfSpeechOptions={labels.partOfSpeech ?? []}
          sttsOptions={(labels.stts ?? []).map((o) => ({
            value: o.value,
            label: `${o.value} – ${o.label}`,
          }))}
          dirtyCount={edits.size}
          onSaveGrid={save}
          onApplied={() => {
            void queryClient.invalidateQueries({ queryKey: lemmaKeys.chapter(bookId, chapter) });
          }}
        />
      )}
      {canEdit && (
        <SaveBar
          count={edits.size}
          status={saveStatus}
          savedCount={savedCount}
          hint="Strg+S"
          saveLabel={t("lemmas.save.save")}
          disabled={incomplete > 0 || wordsById.size === 0}
          message={
            saveStatus === "error"
              ? saveMessage
              : incomplete > 0
                ? t("lemmas.save.incomplete", { count: incomplete })
                : ""
          }
          onSave={() => void save()}
          onDiscard={() => {
            setEdits(NO_EDITS);
            setSaveStatus("idle");
            setSaveMessage("");
          }}
        />
      )}
    </>
  );
}
