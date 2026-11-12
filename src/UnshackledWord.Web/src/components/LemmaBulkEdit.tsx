// biome-ignore-all lint/a11y/useSemanticElements: the virtualized listbox needs div-based options (no <select>)
// biome-ignore-all lint/a11y/useKeyWithClickEvents: keyboard handling is delegated to the listbox's onKeyDown
// biome-ignore-all lint/a11y/noStaticElementInteractions: rows are options of a listbox driven by aria-activedescendant
import { keepPreviousData, useQueries, useQuery, useQueryClient } from "@tanstack/react-query";
import { useVirtualizer } from "@tanstack/react-virtual";
import type { TFunction } from "i18next";
import {
  type KeyboardEvent,
  type ReactNode,
  useEffect,
  useId,
  useLayoutEffect,
  useMemo,
  useRef,
  useState,
} from "react";
import { useTranslation } from "react-i18next";
import { ApiError } from "@/lib/api";
import { bookName } from "@/lib/bible/books";
import type { BibleStructureBook } from "@/lib/bible/types";
import {
  addFrom,
  type BulkQuery,
  type BulkScope,
  clampPage,
  defaultSelection,
  deselectedOriginalCount,
  deselectRows,
  dropFlips,
  effectiveGroupMode,
  type FetchedIds,
  flattenPage,
  type GroupMode,
  isNavigable,
  isSelected,
  itemId,
  type LeftOut,
  type ListItem,
  leftOutOriginals,
  memberKey,
  membersState,
  normOrigKey,
  origKeyOfGroupKey,
  PAGE_SIZES,
  type PageSize,
  pageAfterSizeChange,
  pageCount,
  pageOfIndex,
  planIds,
  queryProblem,
  type RowRef,
  resolveIds,
  rowsBetween,
  type Selection,
  type StaleInfo,
  selectAll,
  selectedInMembers,
  selectedTotal,
  selectNone,
  setRows,
  staleDetailsOf,
  type Testament,
  toggleMembers,
  togglePage,
  toggleRow,
} from "@/lib/lemmas/bulk";
import { bulkApi } from "@/lib/lemmas/bulkApi";
import type { BulkGroupCount, BulkOccurrence, BulkRevertResponse } from "@/lib/lemmas/bulkTypes";
import { useBulkUrl } from "@/lib/lemmas/useBulkUrl";
import { Button } from "./Button";
import { Dialog } from "./Dialog";
import { EmptyState } from "./EmptyState";
import { FormError } from "./FormError";
import { Select, type SelectOption } from "./Select";
import { TextField } from "./TextField";

/** The backend caps the id list (and an apply) at this many words. */
export const MAX_BULK_WORDS = 20000;

export interface LemmaBulkEditProps {
  /** Lemma of the active grid row, prefilled as source when the dialog opens. */
  initialFrom: string | null;
  /** Chapter shown in the grid: defaults for the scope. */
  bookId: number;
  chapter: number;
  books: readonly BibleStructureBook[];
  partOfSpeechOptions: readonly SelectOption[];
  /** Number of unsaved grid rows; the dialog is blocked while it is above 0. */
  dirtyCount: number;
  /** Saves the grid (existing flow); resolves true when everything was saved. */
  onSaveGrid: () => Promise<boolean>;
  /** After an apply or an undo: the grid should reload. */
  onApplied: () => void;
}

interface Form {
  from: string[];
  fromInput: string;
  to: string;
  pos: string;
  original: string[];
  scope: BulkScope;
  testament: Testament;
  bookId: number;
  chapter: number;
}

interface Change {
  changeSetId: number;
  updated: number;
  query: BulkQuery;
  /** hits of the search that were not applied (deselected) */
  notApplied: number;
  /** original groups with rows left unchanged, for "set another lemma" */
  leftOut: LeftOut[];
  undone?: BulkRevertResponse;
}

type VerseState =
  | { status: "loading" }
  | { status: "error" }
  | { status: "ready"; words: VerseWord[] };
interface VerseWord {
  wordId: number;
  text: string;
}

class TooManyError extends Error {}

function useDebounced<T>(value: T, ms = 250): T {
  const [debounced, setDebounced] = useState(value);
  useEffect(() => {
    const timer = window.setTimeout(() => setDebounced(value), ms);
    return () => window.clearTimeout(timer);
  }, [value, ms]);
  return debounced;
}

function useLemmaCounts(q: string) {
  return useQuery({
    queryKey: ["lemmas", "counts", q],
    queryFn: ({ signal }) => bulkApi.lemmaCounts(q, 50, signal),
    enabled: q.length > 0,
    staleTime: 60_000,
    retry: false,
  });
}

function errorMessage(error: unknown, t: TFunction): string {
  if (error instanceof TooManyError)
    return t("lemmas.bulk.errors.tooMany", { max: MAX_BULK_WORDS });
  if (error instanceof ApiError) {
    if (error.status === 401) return t("lemmas.bulk.errors.session");
    if (error.status === 403) return t("lemmas.bulk.errors.forbidden");
    if (error.status === 404) return t("lemmas.bulk.errors.notFound");
    return t("lemmas.bulk.errors.generic");
  }
  if (error instanceof TypeError) return t("lemmas.bulk.errors.network");
  return t("lemmas.bulk.errors.generic");
}

/**
 * Bulk lemma replace: one dialog in three steps (search, review, done). Dialog, step, search and paging
 * live in the URL (`?dlg=review&page=2&size=100&from=…`), so a review page can be reloaded and shared.
 */
export function LemmaBulkEdit(props: LemmaBulkEditProps) {
  const { url } = useBulkUrl();
  return url.step ? <BulkSession {...props} /> : null;
}

function BulkSession({
  initialFrom,
  bookId,
  chapter,
  books,
  partOfSpeechOptions,
  dirtyCount,
  onSaveGrid,
  onApplied,
}: LemmaBulkEditProps) {
  const { t, i18n } = useTranslation();
  const queryClient = useQueryClient();
  const { url, go, close } = useBulkUrl();
  const step = url.step ?? "search";
  const language = i18n.language;
  const dirty = dirtyCount > 0;

  const [form, setForm] = useState<Form>(() => {
    if (url.query) {
      return {
        ...url.query,
        from: [...url.query.from],
        original: [...url.query.original],
        fromInput: "",
      };
    }
    return {
      from: initialFrom ? [initialFrom] : [],
      fromInput: "",
      to: "",
      pos: "",
      original: [],
      scope: "all",
      testament: books.find((b) => b.id === bookId)?.testament ?? "nt",
      bookId,
      chapter,
    };
  });
  const [sel, setSel] = useState<Selection>(selectAll);
  const [lastIndex, setLastIndex] = useState<number | null>(null);
  const [expanded, setExpanded] = useState<Set<number>>(new Set());
  const [verses, setVerses] = useState<Map<number, VerseState>>(new Map());
  const [stale, setStale] = useState<Map<number, StaleInfo>>(new Map());
  const [collapsed, setCollapsed] = useState<Set<string>>(new Set());
  const [staleOffPage, setStaleOffPage] = useState<Set<number>>(new Set());
  const [staleBanner, setStaleBanner] = useState<Map<number, StaleInfo> | null>(null);
  const [applying, setApplying] = useState(false);
  const [undoing, setUndoing] = useState(false);
  const [error, setError] = useState("");
  const [savingGrid, setSavingGrid] = useState(false);
  const [saveFailed, setSaveFailed] = useState(false);
  const [change, setChange] = useState<Change | null>(null);

  const query = step === "review" ? url.query : null;
  const queryKey = query ? JSON.stringify(query) : "";
  const occ = useQuery({
    queryKey: ["lemmas", "bulk", "occ", queryKey, url.page, url.size],
    queryFn: ({ signal }) => bulkApi.occurrences(query as BulkQuery, url.page, url.size, signal),
    enabled: query !== null && !dirty,
    staleTime: 0,
    gcTime: 0,
    retry: false,
    placeholderData: keepPreviousData,
  });
  const data = occ.data;
  const groups = data?.groups ?? [];
  const total = data?.total ?? 0;

  // A new search starts with everything selected.
  // biome-ignore lint/correctness/useExhaustiveDependencies: reset only when the search itself changes
  useEffect(() => {
    setSel(selectAll());
    setLastIndex(null);
    setExpanded(new Set());
    setStale(new Map());
    setCollapsed(new Set());
    setStaleOffPage(new Set());
    setStaleBanner(null);
    setError("");
  }, [queryKey]);

  // Grouping: an explicit choice in the URL, else by original when the server says it is ambiguous.
  const mode: GroupMode = effectiveGroupMode(url.group, data?.ambiguous);
  const byOriginal = data?.byOriginal ?? [];

  // Starting selection once the first answer of a search arrives: flagged groups are deselected
  // when the server reports the lemma as ambiguous.
  const defaultsFor = useRef("");
  useEffect(() => {
    if (!data || occ.isPlaceholderData || defaultsFor.current === queryKey) return;
    defaultsFor.current = queryKey;
    setSel(defaultSelection(data.groups, data.byOriginal ?? [], data.ambiguous === true));
  }, [data, occ.isPlaceholderData, queryKey]);

  // A shared link may point behind the last page: show the last one.
  useEffect(() => {
    if (!query || !data || occ.isPlaceholderData) return;
    if (data.total > 0 && url.page > data.totalPages) {
      go(
        { step: "review", query, page: clampPage(url.page, data.total, url.size), size: url.size },
        true,
      );
    }
  }, [query, data, occ.isPlaceholderData, url.page, url.size, go]);

  // A reloaded "done" link has no result to show.
  useEffect(() => {
    if (step === "done" && !change) close(true);
  }, [step, change, close]);

  const items = useMemo(
    () =>
      data
        ? flattenPage(data.occurrences, data.groups, expanded, {
            mode,
            byOriginal: data.byOriginal,
            collapsed,
          })
        : [],
    [data, expanded, mode, collapsed],
  );
  const pageRows = useMemo(
    () =>
      items.flatMap((item) =>
        item.kind === "row" ? [{ id: item.row.wordId, key: item.key } satisfies RowRef] : [],
      ),
    [items],
  );
  const selected = Math.max(0, selectedTotal(sel, groups) - staleOffPage.size);
  const deselectedOriginals = deselectedOriginalCount(sel, groups);
  const contentOriginals = byOriginal.filter((o) => o.key !== "added" && o.key !== "none");

  function goSearch() {
    go({ step: "search" });
  }

  function submitSearch() {
    const next: BulkQuery = {
      from: form.from,
      to: form.to.trim(),
      pos: form.pos,
      original: form.original,
      scope: form.scope,
      testament: form.testament,
      bookId: form.bookId,
      chapter: form.chapter,
    };
    if (queryProblem(next)) return;
    setChange(null);
    go({ step: "review", query: next, page: 1, size: url.size });
  }

  function toggleRowAt(index: number, shift: boolean) {
    const target = pageRows[index];
    if (!target) return;
    if (shift && lastIndex !== null) {
      const on = !isSelected(sel, target.id, target.key);
      setSel(setRows(sel, rowsBetween(pageRows, lastIndex, index), on));
    } else {
      setSel(toggleRow(sel, target));
    }
    setLastIndex(index);
  }

  async function toggleVerse(row: BulkOccurrence) {
    if (expanded.has(row.wordId)) {
      setExpanded((current) => {
        const next = new Set(current);
        next.delete(row.wordId);
        return next;
      });
      return;
    }
    setExpanded((current) => new Set(current).add(row.wordId));
    if (verses.get(row.wordId)?.status === "ready") return;
    setVerses((current) => new Map(current).set(row.wordId, { status: "loading" }));
    try {
      const verse = await queryClient.fetchQuery({
        queryKey: ["lemmas", "verse", row.bookId, row.chapter, row.verse],
        queryFn: ({ signal }) => bulkApi.verse(row.bookId, row.chapter, row.verse, signal),
        staleTime: 5 * 60_000,
      });
      const words = verse.words.map((w) => ({ wordId: w.wordId, text: w.wordInContext }));
      setVerses((current) => new Map(current).set(row.wordId, { status: "ready", words }));
    } catch {
      setVerses((current) => new Map(current).set(row.wordId, { status: "error" }));
    }
  }

  async function materialiseIds(
    searchQuery: BulkQuery,
  ): Promise<{ wordIds: number[]; expectedOriginal: string[] }> {
    const plan = planIds(sel, groups);
    const fetched: FetchedIds[] = [];
    const take = (response: { truncated: boolean; wordIds: number[] }, origKey: string) => {
      if (response.truncated) throw new TooManyError();
      fetched.push({ ids: response.wordIds, origKey });
    };
    if (plan.fetchAll) take(await bulkApi.wordIds(searchQuery), plan.allOrigKey);
    else {
      for (const key of plan.fetchGroups) {
        const group = groups.find((g) => memberKey(g) === key);
        if (!group?.plainWord || !group.partOfSpeech) throw new Error("group without form");
        const origKey = normOrigKey(group.origKey);
        take(
          await bulkApi.wordIds(searchQuery, {
            plainWord: group.plainWord,
            pos: group.partOfSpeech,
            original: origKey,
          }),
          origKey,
        );
      }
    }
    const resolved = resolveIds(plan, fetched);
    const keep = resolved.wordIds.map((id) => !stale.has(id));
    const wordIds = resolved.wordIds.filter((_, i) => keep[i]);
    if (wordIds.length > MAX_BULK_WORDS) throw new TooManyError();
    return { wordIds, expectedOriginal: resolved.expectedOriginal.filter((_, i) => keep[i]) };
  }

  async function apply() {
    if (!query || applying) return;
    setApplying(true);
    setError("");
    try {
      const { wordIds, expectedOriginal } = await materialiseIds(query);
      if (wordIds.length === 0) return;
      const leftOut = leftOutOriginals(sel, groups, byOriginal);
      const result = await bulkApi.apply({
        from: query.from,
        to: query.to,
        wordIds,
        expectedOriginal,
      });
      setChange({
        changeSetId: result.changeSetId,
        updated: result.updated,
        query,
        notApplied: Math.max(0, total - result.updated),
        leftOut,
      });
      go({ step: "done" }, true);
      onApplied();
    } catch (caught) {
      if (caught instanceof ApiError && caught.status === 409) await handleStale(caught, query);
      else setError(errorMessage(caught, t));
    } finally {
      setApplying(false);
    }
  }

  async function handleStale(conflict: ApiError, searchQuery: BulkQuery) {
    let body: unknown = null;
    try {
      body = JSON.parse(conflict.body);
    } catch {
      // no usable body: fall through with an empty list
    }
    const details = staleDetailsOf(body);
    const ids = [...details.keys()];
    if (ids.length === 0) {
      setError(errorMessage(conflict, t));
      return;
    }
    const onPage = new Map(pageRows.map((row, i) => [row.id, i]));
    const pageStale = ids.flatMap((id) =>
      onPage.has(id) ? [pageRows[onPage.get(id) as number] as RowRef] : [],
    );
    const off = ids.filter((id) => !onPage.has(id) && !sel.flips.has(id));
    setSel(
      dropFlips(
        deselectRows(sel, pageStale),
        ids.filter((id) => !onPage.has(id)),
      ),
    );
    setStale((current) => new Map([...current, ...details]));
    setStaleOffPage((current) => new Set([...current, ...off]));
    setStaleBanner(details);
    if (pageStale.length === 0) await jumpToStale(searchQuery, ids);
  }

  async function jumpToStale(searchQuery: BulkQuery, ids: number[]) {
    const first = new Set(ids);
    const onPage = pageRows.findIndex((row) => first.has(row.id));
    if (onPage >= 0) return;
    try {
      const all = await bulkApi.wordIds(searchQuery);
      const index = all.wordIds.findIndex((id) => first.has(id));
      if (index >= 0)
        go({
          step: "review",
          query: searchQuery,
          page: pageOfIndex(index, url.size),
          size: url.size,
        });
    } catch {
      // the banner already explains the conflict
    }
  }

  async function undo() {
    if (!change || undoing || change.undone) return;
    setUndoing(true);
    setError("");
    try {
      const result = await bulkApi.revert(change.changeSetId);
      setChange({ ...change, undone: result });
      onApplied();
    } catch (caught) {
      setError(
        caught instanceof ApiError && caught.status === 409
          ? t("lemmas.bulk.errors.alreadyReverted")
          : errorMessage(caught, t),
      );
    } finally {
      setUndoing(false);
    }
  }

  const leftOutLabel = (item: LeftOut) =>
    item.key === "added" || item.key === "none"
      ? t(`lemmas.bulk.review.original.${item.key}`)
      : (item.word ?? item.key);

  /** Starts a new run for rows left out of a finished one: same source, empty target, original preset. */
  function setAnotherLemma(done: Change, keys: string[]) {
    const { query: previous } = done;
    setForm({
      from: [...previous.from],
      fromInput: "",
      to: "",
      pos: previous.pos,
      original: keys,
      scope: previous.scope,
      testament: previous.testament,
      bookId: previous.bookId,
      chapter: previous.chapter,
    });
    go({ step: "search" });
  }

  async function saveAndContinue() {
    setSavingGrid(true);
    setSaveFailed(false);
    const ok = await onSaveGrid();
    setSavingGrid(false);
    setSaveFailed(!ok);
  }

  const steps = (
    <ol aria-label={t("lemmas.bulk.steps.label")} className="flex gap-4">
      {(["search", "review", "done"] as const).map((name, i) => (
        <li
          key={name}
          aria-current={step === name ? "step" : undefined}
          className={step === name ? "font-semibold text-fg" : "text-fg-muted"}
        >
          {i + 1} {t(`lemmas.bulk.steps.${name}`)}
        </li>
      ))}
    </ol>
  );

  let body: ReactNode;
  let footer: ReactNode;
  const bare = step === "review" && !dirty;

  if (dirty && step !== "done") {
    body = (
      <div role="alert" className="grid gap-3 rounded-lg border border-accent p-4">
        <p className="font-medium">{t("lemmas.bulk.dirty.title", { count: dirtyCount })}</p>
        <p className="text-sm text-fg-muted">{t("lemmas.bulk.dirty.text")}</p>
        <FormError message={saveFailed ? t("lemmas.bulk.dirty.failed") : ""} />
        <div className="flex gap-2">
          <Button variant="primary" disabled={savingGrid} onClick={() => void saveAndContinue()}>
            {savingGrid ? t("lemmas.bulk.dirty.saving") : t("lemmas.bulk.dirty.saveContinue")}
          </Button>
          <Button onClick={() => close()}>{t("lemmas.bulk.dirty.cancel")}</Button>
        </div>
      </div>
    );
    footer = <span className="text-sm text-fg-muted">{t("lemmas.bulk.dirty.hint")}</span>;
  } else if (step === "search") {
    const problem = queryProblem({ ...form, to: form.to.trim() } as BulkQuery);
    body = (
      <SearchStep
        form={form}
        setForm={setForm}
        books={books}
        language={language}
        partOfSpeechOptions={partOfSpeechOptions}
        onSubmit={submitSearch}
      />
    );
    footer = (
      <>
        <span className="grow text-sm text-fg-muted">
          {problem ? t("lemmas.bulk.search.footNote") : ""}
        </span>
        <Button onClick={() => close()}>{t("lemmas.bulk.search.cancel")}</Button>
        <Button variant="primary" disabled={problem !== null} onClick={submitSearch}>
          {t("lemmas.bulk.search.go")}
        </Button>
      </>
    );
  } else if (step === "review" && query) {
    const scopeLabel = scopeText(query, books, language, t);
    const summary = (
      <div className="flex flex-wrap items-center gap-2 border-b border-border px-4 py-2 text-sm">
        {query.from.map((lemma) => (
          <Chip key={lemma}>{lemma}</Chip>
        ))}
        <span aria-hidden="true">→</span>
        <Chip>{query.to}</Chip>
        <span className="text-fg-muted">
          {query.pos || t("lemmas.bulk.review.allPos")} · {scopeLabel}
        </span>
        <span className="grow" />
        <Button size="sm" onClick={goSearch}>
          {t("lemmas.bulk.review.changeSearch")}
        </Button>
      </div>
    );
    if (occ.isError && !data) {
      body = (
        <>
          {summary}
          <div className="p-4">
            <EmptyState title={t("lemmas.bulk.review.errorTitle")}>
              <p className="mb-3">{errorMessage(occ.error, t)}</p>
              <div className="flex gap-2">
                <Button variant="primary" onClick={() => void occ.refetch()}>
                  {t("lemmas.bulk.review.retry")}
                </Button>
                <Button onClick={goSearch}>{t("lemmas.bulk.review.changeSearch")}</Button>
              </div>
            </EmptyState>
          </div>
        </>
      );
      footer = <span className="grow" />;
    } else if (!data) {
      body = (
        <>
          {summary}
          <div aria-busy="true" role="status" className="grid gap-2 p-4">
            <p className="sr-only">{t("lemmas.bulk.review.loading")}</p>
            {[0, 1, 2, 3, 4, 5].map((n) => (
              <div key={n} aria-hidden="true" className="h-6 rounded-sm bg-surface-muted" />
            ))}
          </div>
        </>
      );
      footer = (
        <>
          <span className="grow text-sm text-fg-muted">{t("lemmas.bulk.review.loadingFoot")}</span>
          <Button onClick={goSearch}>{t("lemmas.bulk.search.cancel")}</Button>
        </>
      );
    } else if (data.total === 0) {
      body = (
        <>
          {summary}
          <div className="p-4">
            <EmptyState title={t("lemmas.bulk.review.emptyTitle")}>
              <p className="mb-3">
                {t("lemmas.bulk.review.emptyText", { from: query.from.join(", ") })}
              </p>
              <Button variant="primary" onClick={goSearch}>
                {t("lemmas.bulk.review.changeSearch")}
              </Button>
            </EmptyState>
          </div>
        </>
      );
      footer = <span className="grow" />;
    } else {
      const pages = pageCount(data.total, url.size);
      const from = (url.page - 1) * url.size + 1;
      const to = Math.min(data.total, url.page * url.size);
      const toPage = (page: number) =>
        go({ step: "review", query, page: clampPage(page, data.total, url.size), size: url.size });
      body = (
        <>
          {summary}
          {data.ambiguous && (
            <p role="note" className="border-b border-border px-4 py-2 text-sm">
              <span aria-hidden="true" className="font-medium text-accent">
                !{" "}
              </span>
              {t("lemmas.bulk.review.ambiguous", {
                lemma: query.from.join(", "),
                count: contentOriginals.length,
                names: contentOriginals
                  .slice(0, 4)
                  .map((o) => o.word ?? o.key)
                  .join(", "),
              })}
            </p>
          )}
          {staleBanner && (
            <div
              role="alert"
              className="m-3 grid gap-2 rounded-lg border border-accent p-3 text-sm"
            >
              <p className="font-medium">
                <span aria-hidden="true">! </span>
                {t("lemmas.bulk.review.staleTitle", { count: staleBanner.size })}
              </p>
              {[...staleBanner.values()].some((info) => info.reason === "lemma") && (
                <p>{t("lemmas.bulk.review.staleText", { from: query.from.join("/") })}</p>
              )}
              {[...staleBanner.values()].some((info) => info.reason === "original") && (
                <p>{t("lemmas.bulk.review.staleTextOriginal")}</p>
              )}
              <div className="flex gap-2">
                <Button size="sm" onClick={() => void jumpToStale(query, [...staleBanner.keys()])}>
                  {t("lemmas.bulk.review.gotoStale")}
                </Button>
                <Button size="sm" onClick={() => setStaleBanner(null)}>
                  {t("lemmas.bulk.review.dismissStale")}
                </Button>
              </div>
            </div>
          )}
          <div className="flex flex-wrap items-center gap-x-3 gap-y-2 border-b border-border px-4 py-2 text-sm">
            <span className="flex items-center gap-1">
              <span className="text-fg-muted">{t("lemmas.bulk.review.groupBy")}</span>
              {(["form", "original"] as const).map((option) => (
                <Button
                  key={option}
                  size="sm"
                  aria-pressed={mode === option}
                  onClick={() =>
                    go({
                      step: "review",
                      query,
                      page: url.page,
                      size: url.size,
                      group: option,
                    })
                  }
                >
                  {t(`lemmas.bulk.review.groupModes.${option}`)}
                </Button>
              ))}
            </span>
            <nav aria-label={t("lemmas.bulk.review.pages")} className="flex items-center gap-1">
              <Button
                size="sm"
                disabled={url.page <= 1}
                aria-label={t("lemmas.bulk.review.first")}
                onClick={() => toPage(1)}
              >
                «
              </Button>
              <Button
                size="sm"
                disabled={url.page <= 1}
                aria-label={t("lemmas.bulk.review.previous")}
                onClick={() => toPage(url.page - 1)}
              >
                ‹
              </Button>
              <span aria-live="polite" className="px-1">
                {t("lemmas.bulk.review.pageOf", { page: url.page, pages })}
              </span>
              <Button
                size="sm"
                disabled={url.page >= pages}
                aria-label={t("lemmas.bulk.review.next")}
                onClick={() => toPage(url.page + 1)}
              >
                ›
              </Button>
              <Button
                size="sm"
                disabled={url.page >= pages}
                aria-label={t("lemmas.bulk.review.last")}
                onClick={() => toPage(pages)}
              >
                »
              </Button>
            </nav>
            <Select
              label={t("lemmas.bulk.review.pageSize")}
              value={String(url.size)}
              options={PAGE_SIZES.map((n) => ({ value: String(n), label: String(n) }))}
              onChange={(value) => {
                const size = Number(value) as PageSize;
                go({
                  step: "review",
                  query,
                  page: pageAfterSizeChange(url.page, url.size, size),
                  size,
                });
              }}
            />
            <span className="text-fg-muted">
              {t("lemmas.bulk.review.rows", { from, to, total: data.total })}
            </span>
            <span className="grow" />
            <Button size="sm" onClick={() => setSel(togglePage(sel, pageRows))}>
              {t("lemmas.bulk.review.selectPage")}
            </Button>
            <Button size="sm" onClick={() => setSel(selectAll())}>
              {t("lemmas.bulk.review.selectAll", { total: data.total })}
            </Button>
            <Button size="sm" onClick={() => setSel(selectNone())}>
              {t("lemmas.bulk.review.selectNone")}
            </Button>
          </div>
          <ReviewList
            key={`${url.page}:${url.size}`}
            items={items}
            sel={sel}
            to={query.to}
            stale={stale}
            expanded={expanded}
            verses={verses}
            books={books}
            language={language}
            disabled={applying}
            onToggleRow={toggleRowAt}
            onToggleMembers={(members) => setSel(toggleMembers(sel, members))}
            onToggleCollapse={(key) =>
              setCollapsed((current) => {
                const next = new Set(current);
                if (!next.delete(key)) next.add(key);
                return next;
              })
            }
            onToggleVerse={(row) => void toggleVerse(row)}
            onSelectAll={() => setSel(selectAll())}
            onSelectNone={() => setSel(selectNone())}
          />
        </>
      );
      footer = (
        <>
          <span aria-live="polite" className="grow text-sm">
            <b>{t("lemmas.bulk.review.selected", { selected, total: data.total })}</b>{" "}
            <span className="text-fg-muted">{t("lemmas.bulk.review.allPages")}</span>
            {deselectedOriginals > 0 && (
              <span className="text-fg-muted">
                {" · "}
                {t("lemmas.bulk.review.originalsOff", { count: deselectedOriginals })}
              </span>
            )}
          </span>
          <FormError message={error} />
          <Button onClick={goSearch}>{t("lemmas.bulk.review.back")}</Button>
          <Button
            variant="primary"
            disabled={selected === 0 || applying}
            onClick={() => void apply()}
          >
            {applying
              ? t("lemmas.bulk.review.applying")
              : t("lemmas.bulk.review.apply", { count: selected })}
          </Button>
        </>
      );
    }
  } else {
    const done = change;
    body = done ? (
      <div role="status" className="grid gap-3">
        <h3 className="text-base font-semibold">
          {done.undone
            ? t("lemmas.bulk.done.reverted", { count: done.undone.restored })
            : t("lemmas.bulk.done.applied", { count: done.updated })}
        </h3>
        <dl className="grid grid-cols-2 gap-x-4 gap-y-1 text-sm">
          <dt className="text-fg-muted">{t("lemmas.bulk.done.changeSet")}</dt>
          <dd>
            #{done.changeSetId}
            {done.undone && done.undone.changeSetId > 0
              ? ` · ${t("lemmas.bulk.done.undoneAs", { id: done.undone.changeSetId })}`
              : ""}
          </dd>
          <dt className="text-fg-muted">{t("lemmas.bulk.done.replaced")}</dt>
          <dd>
            {done.query.from.join(", ")} → <b>{done.query.to}</b>
          </dd>
          <dt className="text-fg-muted">{t("lemmas.bulk.done.notApplied")}</dt>
          <dd>{t("lemmas.bulk.done.notAppliedValue", { count: done.notApplied })}</dd>
          {done.undone && done.undone.skipped > 0 && (
            <>
              <dt className="text-fg-muted">{t("lemmas.bulk.done.skipped")}</dt>
              <dd className="text-accent">
                {t("lemmas.bulk.done.skippedValue", { count: done.undone.skipped })}
              </dd>
            </>
          )}
        </dl>
        {done.undone && done.undone.restored === 0 && (
          <p className="text-sm">{t("lemmas.bulk.done.nothingRestored")}</p>
        )}
        {done.leftOut.length > 0 && !done.undone && (
          <div className="grid gap-2 text-sm">
            <p>
              <b>{t("lemmas.bulk.done.leftOut")}</b>{" "}
              {done.leftOut.map((l) => `${leftOutLabel(l)} (${l.count})`).join(", ")}
            </p>
            <div className="flex flex-wrap gap-2">
              {done.leftOut.map((l) => (
                <Button key={l.key} size="sm" onClick={() => setAnotherLemma(done, [l.key])}>
                  {t("lemmas.bulk.done.setOther", { name: leftOutLabel(l), count: l.count })}
                </Button>
              ))}
              {done.leftOut.length > 1 && (
                <Button
                  size="sm"
                  onClick={() =>
                    setAnotherLemma(
                      done,
                      done.leftOut.map((l) => l.key),
                    )
                  }
                >
                  {t("lemmas.bulk.done.setOtherAll", {
                    count: done.leftOut.reduce((sum, l) => sum + l.count, 0),
                  })}
                </Button>
              )}
            </div>
          </div>
        )}
        <p className="text-sm text-fg-muted">{t("lemmas.bulk.done.note")}</p>
        <FormError message={error} />
      </div>
    ) : null;
    footer = (
      <>
        <span className="grow" />
        <Button disabled={!done || !!done.undone || undoing} onClick={() => void undo()}>
          {undoing
            ? t("lemmas.bulk.done.undoing")
            : done?.undone
              ? t("lemmas.bulk.done.undone")
              : t("lemmas.bulk.done.undo")}
        </Button>
        <Button variant="primary" onClick={() => close()}>
          {t("lemmas.bulk.done.close")}
        </Button>
      </>
    );
  }

  return (
    <Dialog
      open
      size="lg"
      bare={bare}
      title={t("lemmas.bulk.title")}
      description={steps}
      dismissible={!applying && !undoing}
      footer={footer}
      onClose={() => close()}
    >
      {body}
    </Dialog>
  );
}

function scopeText(
  query: BulkQuery,
  books: readonly BibleStructureBook[],
  language: string,
  t: TFunction,
): string {
  const book = books.find((b) => b.id === query.bookId);
  const name = book ? bookName(book, language) : "";
  if (query.scope === "chapter") return `${name} ${query.chapter}`;
  if (query.scope === "book") return name;
  if (query.scope === "testament") return t(`lemmas.bulk.search.testaments.${query.testament}`);
  return t("lemmas.bulk.search.scopes.all");
}

function Chip({ children }: { children: ReactNode }) {
  return (
    <span className="inline-flex items-center gap-1 rounded-md border border-border bg-surface-muted px-2 py-0.5 text-sm">
      {children}
    </span>
  );
}

function SearchStep({
  form,
  setForm,
  books,
  language,
  partOfSpeechOptions,
  onSubmit,
}: {
  form: Form;
  setForm: (update: (form: Form) => Form) => void;
  books: readonly BibleStructureBook[];
  language: string;
  partOfSpeechOptions: readonly SelectOption[];
  onSubmit: () => void;
}) {
  const { t } = useTranslation();
  const scopeId = useId();
  const toRef = useRef<HTMLInputElement>(null);
  const fromRef = useRef<HTMLInputElement>(null);
  // Focus once when the step opens: the target field if a source is prefilled.
  // biome-ignore lint/correctness/useExhaustiveDependencies: only on mount
  useEffect(() => {
    (form.from.length > 0 ? toRef : fromRef).current?.focus();
  }, []);

  const patch = (next: Partial<Form>) => setForm((current) => ({ ...current, ...next }));

  const fromQuery = useDebounced(form.fromInput.trim() || (form.from[0] ?? "").slice(0, 3));
  const toTrimmed = form.to.trim();
  const toQuery = useDebounced(toTrimmed);
  const fromSuggestions = useLemmaCounts(fromQuery);
  const toCounts = useLemmaCounts(toQuery);
  const toSimilar = useLemmaCounts(toQuery.slice(0, 3));
  const chipCounts = useQueries({
    queries: form.from.map((lemma) => ({
      queryKey: ["lemmas", "counts", lemma],
      queryFn: ({ signal }: { signal: AbortSignal }) => bulkApi.lemmaCounts(lemma, 50, signal),
      staleTime: 60_000,
      retry: false,
    })),
  });

  const exact = (lemma: string, items?: { lemma: string; count: number }[]) =>
    items?.find((item) => item.lemma === lemma)?.count;
  const problem = queryProblem({ ...form, to: toTrimmed } as BulkQuery);

  // The preview ignores the original filter: its groups fill that very filter.
  const previewQuery: BulkQuery = { ...form, to: toTrimmed, original: [] };
  const preview = useQuery({
    queryKey: ["lemmas", "bulk", "preview", JSON.stringify({ ...previewQuery, to: "" })],
    queryFn: ({ signal }) => bulkApi.occurrences(previewQuery, 1, 50, signal),
    enabled:
      form.from.length > 0 &&
      (form.scope === "all" || form.scope === "testament" || form.bookId > 0),
    staleTime: 10_000,
    retry: false,
  });

  const previewOriginals = (preview.data?.byOriginal ?? []).filter(
    (o) => o.key !== "added" && o.key !== "none",
  );
  const originalOptions: SelectOption[] = [
    { value: "", label: t("lemmas.bulk.search.originalAll") },
    ...(preview.data?.byOriginal ?? []).map((o) => ({
      value: o.key,
      label:
        o.key === "added" || o.key === "none"
          ? `${t(`lemmas.bulk.review.original.${o.key}`)} ${o.count}`
          : `${o.key}${o.word ? ` ${o.word}` : ""}${o.gloss ? ` »${o.gloss}«` : ""} ${o.count}`,
    })),
  ];
  // A preset original (e.g. from "set another lemma") stays selectable before the preview arrived.
  for (const key of form.original) {
    if (!originalOptions.some((option) => option.value === key)) {
      originalOptions.push({ value: key, label: key });
    }
  }
  const book = books.find((b) => b.id === form.bookId);
  const chapterMax = book?.versesPerChapter.length ?? 1;
  const bookOptions = books.map((b) => ({ value: String(b.id), label: bookName(b, language) }));
  const related = (fromSuggestions.data?.items ?? [])
    .filter((item) => !form.from.includes(item.lemma) && item.lemma !== toTrimmed)
    .slice(0, 6);
  const similar = (toSimilar.data?.items ?? [])
    .filter((item) => item.lemma !== toTrimmed && !form.from.includes(item.lemma))
    .slice(0, 5);

  let toStatus = t("lemmas.bulk.search.toNone");
  let toWarn = false;
  if (toTrimmed) {
    if (form.from.includes(toTrimmed)) {
      toStatus = t("lemmas.bulk.search.toSame");
      toWarn = true;
    } else if (toCounts.data && toQuery === toTrimmed) {
      const count = exact(toTrimmed, toCounts.data.items);
      toWarn = count === undefined;
      toStatus =
        count === undefined
          ? t("lemmas.bulk.search.toNew", { lemma: toTrimmed })
          : t("lemmas.bulk.search.toExists", { lemma: toTrimmed, count });
    } else toStatus = "";
  }

  return (
    <div className="grid gap-4">
      <div className="grid gap-4 md:grid-cols-2">
        <div className="grid content-start gap-1">
          <span className="text-sm font-medium">{t("lemmas.bulk.search.from")}</span>
          <div className="flex flex-wrap items-center gap-2">
            {form.from.map((lemma, i) => (
              <Chip key={lemma}>
                {lemma}
                <span className="text-fg-muted">
                  {chipCounts[i]?.data ? (exact(lemma, chipCounts[i].data?.items) ?? 0) : ""}
                </span>
                <Button
                  variant="ghost"
                  size="sm"
                  className="px-1 py-0"
                  aria-label={t("lemmas.bulk.search.removeFrom", { lemma })}
                  onClick={() => patch({ from: form.from.filter((l) => l !== lemma) })}
                >
                  ✕
                </Button>
              </Chip>
            ))}
            <TextField
              label={t("lemmas.bulk.search.fromAdd")}
              labelHidden
              size="sm"
              required={false}
              autoComplete="off"
              inputRef={fromRef}
              placeholder={
                form.from.length
                  ? t("lemmas.bulk.search.fromMore")
                  : t("lemmas.bulk.search.fromFirst")
              }
              value={form.fromInput}
              onChange={(fromInput) => patch({ fromInput })}
              onKeyDown={(event) => {
                if (event.key === "Enter") {
                  event.preventDefault();
                  const next = addFrom(form.from, form.fromInput);
                  if (next.length !== form.from.length) patch({ from: next, fromInput: "" });
                  else if (
                    !form.fromInput.trim() &&
                    !queryProblem({ ...form, to: toTrimmed } as BulkQuery)
                  )
                    onSubmit();
                } else if (event.key === "Backspace" && !form.fromInput && form.from.length) {
                  patch({ from: form.from.slice(0, -1) });
                }
              }}
            />
          </div>
          {related.length > 0 && (
            <div className="flex flex-wrap items-center gap-2 text-sm" aria-live="polite">
              <span className="text-fg-muted">
                {form.fromInput.trim()
                  ? t("lemmas.bulk.search.suggestions")
                  : t("lemmas.bulk.search.related")}
              </span>
              {related.map((item) => (
                <Button
                  key={item.lemma}
                  size="sm"
                  title={t("lemmas.bulk.search.addTitle")}
                  onClick={() => patch({ from: addFrom(form.from, item.lemma), fromInput: "" })}
                >
                  {item.lemma} {item.count} +
                </Button>
              ))}
            </div>
          )}
          <p className="text-sm text-fg-muted">{t("lemmas.bulk.search.fromHelp")}</p>
          {preview.data?.ambiguous && (
            <p role="note" className="text-sm font-medium text-accent">
              <span aria-hidden="true">! </span>
              {t("lemmas.bulk.search.ambiguous", {
                lemma: form.from.join(", "),
                count: previewOriginals.length,
                names: previewOriginals
                  .slice(0, 4)
                  .map((o) => o.word ?? o.key)
                  .join(", "),
              })}
            </p>
          )}
        </div>
        <div className="grid content-start gap-1">
          <TextField
            label={t("lemmas.bulk.search.to")}
            size="sm"
            required={false}
            autoComplete="off"
            inputRef={toRef}
            value={form.to}
            onChange={(to) => patch({ to })}
            onKeyDown={(event) => {
              if (event.key === "Enter" && !problem) {
                event.preventDefault();
                onSubmit();
              }
            }}
          />
          <p
            aria-live="polite"
            className={`text-sm ${toWarn ? "font-medium text-accent" : "text-fg-muted"}`}
          >
            {toStatus}
          </p>
          {similar.length > 0 && (
            <div className="flex flex-wrap items-center gap-2 text-sm">
              <span className="text-fg-muted">{t("lemmas.bulk.search.similar")}</span>
              {similar.map((item) => (
                <Button key={item.lemma} size="sm" onClick={() => patch({ to: item.lemma })}>
                  {item.lemma} {item.count}
                </Button>
              ))}
            </div>
          )}
        </div>
      </div>
      <div className="grid gap-4 md:grid-cols-2">
        <div className="grid content-start gap-1">
          <Select
            label={t("lemmas.bulk.search.pos")}
            value={form.pos}
            options={[{ value: "", label: t("lemmas.bulk.search.posAll") }, ...partOfSpeechOptions]}
            onChange={(pos) => patch({ pos })}
          />
          <p className="text-sm text-fg-muted">{t("lemmas.bulk.search.posHelp")}</p>
          <Select
            label={t("lemmas.bulk.search.original")}
            value={form.original[0] ?? ""}
            options={originalOptions}
            onChange={(value) => patch({ original: value ? [value] : [] })}
          />
          <p className="text-sm text-fg-muted">{t("lemmas.bulk.search.originalHelp")}</p>
        </div>
        <fieldset className="grid content-start gap-2">
          <legend className="mb-1 text-sm font-medium">{t("lemmas.bulk.search.scope")}</legend>
          <div className="flex flex-wrap gap-x-4 gap-y-1 text-sm">
            {(["chapter", "book", "testament", "all"] as const).map((scope) => (
              <label key={scope} className="flex items-center gap-1">
                <input
                  type="radio"
                  name={scopeId}
                  checked={form.scope === scope}
                  onChange={() => patch({ scope })}
                />
                {t(`lemmas.bulk.search.scopes.${scope}`)}
              </label>
            ))}
          </div>
          <div className="flex flex-wrap items-center gap-2">
            {form.scope === "testament" && (
              <Select
                label={t("lemmas.bulk.search.testament")}
                value={form.testament}
                options={(["ot", "nt"] as const).map((v) => ({
                  value: v,
                  label: t(`lemmas.bulk.search.testaments.${v}`),
                }))}
                onChange={(value) => patch({ testament: value === "ot" ? "ot" : "nt" })}
              />
            )}
            {(form.scope === "book" || form.scope === "chapter") && (
              <Select
                label={t("lemmas.bulk.search.book")}
                value={String(form.bookId)}
                options={bookOptions}
                onChange={(value) => patch({ bookId: Number(value), chapter: 1 })}
              />
            )}
            {form.scope === "chapter" && (
              <TextField
                label={t("lemmas.bulk.search.chapter")}
                size="sm"
                required={false}
                type="number"
                min={1}
                max={chapterMax}
                value={String(form.chapter)}
                onChange={(value) =>
                  patch({
                    chapter: Math.min(chapterMax, Math.max(1, Number.parseInt(value, 10) || 1)),
                  })
                }
              />
            )}
          </div>
        </fieldset>
      </div>
      <p aria-live="polite" className="text-sm">
        {form.from.length === 0
          ? t("lemmas.bulk.search.noFrom")
          : preview.data
            ? `${t("lemmas.bulk.search.preview", {
                total: form.original.length
                  ? (preview.data.byOriginal ?? [])
                      .filter((o) => form.original.includes(o.key))
                      .reduce((sum, o) => sum + o.count, 0)
                  : preview.data.total,
              })} (${preview.data.byLemma
                .map((entry) => `${entry.lemma} ${entry.count}`)
                .join(
                  " + ",
                )})${form.pos ? ` · ${t("lemmas.bulk.search.previewPos", { pos: form.pos })}` : ""}`
            : preview.isFetching
              ? t("lemmas.bulk.search.previewLoading")
              : ""}
      </p>
    </div>
  );
}

function TriCheckbox({
  state,
  label,
  tabIndex,
  onToggle,
}: {
  state: "checked" | "unchecked" | "mixed";
  label: string;
  tabIndex?: number;
  onToggle: () => void;
}) {
  const ref = useRef<HTMLInputElement>(null);
  useEffect(() => {
    if (ref.current) ref.current.indeterminate = state === "mixed";
  }, [state]);
  return (
    <input
      ref={ref}
      type="checkbox"
      checked={state === "checked"}
      aria-checked={state === "mixed" ? "mixed" : state === "checked"}
      aria-label={label}
      tabIndex={tabIndex}
      onClick={(event) => event.stopPropagation()}
      onChange={onToggle}
    />
  );
}

const ESTIMATE: Record<ListItem["kind"], number> = { original: 40, group: 36, row: 40, verse: 72 };

function ReviewList({
  items,
  sel,
  to,
  stale,
  expanded,
  verses,
  books,
  language,
  disabled,
  onToggleRow,
  onToggleMembers,
  onToggleCollapse,
  onToggleVerse,
  onSelectAll,
  onSelectNone,
}: {
  items: ListItem[];
  sel: Selection;
  to: string;
  stale: ReadonlyMap<number, StaleInfo>;
  expanded: ReadonlySet<number>;
  verses: ReadonlyMap<number, VerseState>;
  books: readonly BibleStructureBook[];
  language: string;
  disabled: boolean;
  onToggleRow: (index: number, shift: boolean) => void;
  onToggleMembers: (members: BulkGroupCount[]) => void;
  onToggleCollapse: (originalKey: string) => void;
  onToggleVerse: (row: BulkOccurrence) => void;
  onSelectAll: () => void;
  onSelectNone: () => void;
}) {
  const { t } = useTranslation();
  const listId = useId();
  const scrollRef = useRef<HTMLDivElement>(null);
  const firstNav = Math.max(0, items.findIndex(isNavigable));
  const [activeId, setActiveId] = useState<string | null>(null);
  const activeIndex = Math.max(
    0,
    activeId ? items.findIndex((item) => itemId(item) === activeId) : firstNav,
  );
  const rowCount = items.filter((item) => item.kind === "row").length;

  const virtualizer = useVirtualizer({
    count: items.length,
    getScrollElement: () => scrollRef.current,
    estimateSize: (index) => ESTIMATE[items[index]?.kind ?? "row"],
    overscan: 8,
    initialRect: { width: 800, height: 600 },
    getItemKey: (index) => (items[index] ? itemId(items[index]) : index),
    rangeExtractor: (range) => {
      const indexes = new Set<number>();
      for (let i = range.startIndex - range.overscan; i <= range.endIndex + range.overscan; i++) {
        if (i >= 0 && i < range.count) indexes.add(i);
      }
      // Keep the cursor row mounted only once the user has a cursor, not for the default first row.
      if (activeId && items.length > 0) indexes.add(activeIndex);
      return [...indexes].sort((a, b) => a - b);
    },
  });
  const virtualItems = virtualizer.getVirtualItems();
  const padTop = virtualItems[0]?.start ?? 0;
  const padBottom =
    virtualItems.length > 0 ? virtualizer.getTotalSize() - (virtualItems.at(-1)?.end ?? 0) : 0;
  // The kept cursor row can sit apart from the window; a spacer covers the rows between them.
  let gapIndex = -1;
  let gapHeight = 0;
  for (let i = 1; i < virtualItems.length; i++) {
    const previous = virtualItems[i - 1];
    const current = virtualItems[i];
    if (previous && current && current.index !== previous.index + 1) {
      gapIndex = current.index;
      gapHeight = current.start - previous.end;
      break;
    }
  }
  useLayoutEffect(() => {
    const element = scrollRef.current;
    if (!element) return;
    element.style.setProperty("--lemma-pad-top", `${padTop}px`);
    element.style.setProperty("--lemma-pad-bottom", `${Math.max(0, padBottom)}px`);
    element.style.setProperty("--lemma-pad-gap", `${Math.max(0, gapHeight)}px`);
  });

  const abbreviation = (bookId: number) => {
    const book = books.find((b) => b.id === bookId);
    return book
      ? language.startsWith("en")
        ? book.abbreviationEn
        : book.abbreviationDe
      : String(bookId);
  };

  function moveTo(index: number) {
    const item = items[index];
    if (!item) return;
    setActiveId(itemId(item));
    virtualizer.scrollToIndex(index);
  }
  function step(from: number, delta: number): number {
    let index = from;
    let remaining = Math.abs(delta);
    const dir = delta < 0 ? -1 : 1;
    while (remaining > 0) {
      let next = index + dir;
      while (items[next] && !isNavigable(items[next] as ListItem)) next += dir;
      if (!items[next]) break;
      index = next;
      remaining--;
    }
    return index;
  }

  function onKeyDown(event: KeyboardEvent<HTMLDivElement>) {
    if (disabled) return;
    const item = items[activeIndex];
    if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "a") {
      event.preventDefault();
      if (event.shiftKey) onSelectNone();
      else onSelectAll();
      return;
    }
    switch (event.key) {
      case "ArrowDown":
        event.preventDefault();
        moveTo(step(activeIndex, 1));
        break;
      case "ArrowUp":
        event.preventDefault();
        moveTo(step(activeIndex, -1));
        break;
      case "PageDown":
        event.preventDefault();
        moveTo(step(activeIndex, 10));
        break;
      case "PageUp":
        event.preventDefault();
        moveTo(step(activeIndex, -10));
        break;
      case "Home":
        event.preventDefault();
        moveTo(firstNav);
        break;
      case "End":
        event.preventDefault();
        moveTo(step(items.length, -1));
        break;
      case " ":
        event.preventDefault();
        if (item?.kind === "original") onToggleMembers(item.members);
        else if (item?.kind === "group") onToggleMembers(item.group.members);
        else if (item?.kind === "row") onToggleRow(item.index, event.shiftKey);
        break;
      case "Enter":
      case "ArrowRight":
        if (item?.kind === "original" && item.collapsed) {
          event.preventDefault();
          onToggleCollapse(item.key);
        } else if (item?.kind === "row" && !expanded.has(item.row.wordId)) {
          event.preventDefault();
          onToggleVerse(item.row);
        }
        break;
      case "ArrowLeft":
        if (item?.kind === "original" && !item.collapsed) {
          event.preventDefault();
          onToggleCollapse(item.key);
        } else if (item?.kind === "row" && expanded.has(item.row.wordId)) {
          event.preventDefault();
          onToggleVerse(item.row);
        }
        break;
    }
  }

  useEffect(() => {
    scrollRef.current?.focus({ preventScroll: true });
  }, []);

  return (
    <div
      ref={scrollRef}
      role="listbox"
      tabIndex={0}
      aria-multiselectable="true"
      aria-label={t("lemmas.bulk.review.list")}
      aria-activedescendant={items[activeIndex] ? `${listId}-${activeIndex}` : undefined}
      onKeyDown={onKeyDown}
      className="min-h-0 flex-1 overflow-y-auto"
    >
      <div aria-hidden="true" className="lemma-pad-top" />
      {virtualItems.map((virtual) => {
        const item = items[virtual.index];
        if (!item) return null;
        const active = virtual.index === activeIndex;
        const common = {
          "data-index": virtual.index,
          "data-gap": virtual.index === gapIndex ? "" : undefined,
          ref: virtualizer.measureElement,
        };
        if (item.kind === "original") {
          const { original } = item;
          const state = membersState(sel, item.members);
          const chosen = selectedInMembers(sel, item.members);
          const gap = original.key === "added" || original.key === "none";
          const title = gap
            ? t(`lemmas.bulk.review.original.${original.key}`)
            : `${original.key}${original.word ? ` ${original.word}` : ""}`;
          return (
            <div
              key={virtual.key}
              {...common}
              id={`${listId}-${virtual.index}`}
              role="option"
              tabIndex={-1}
              aria-selected={state === "checked"}
              data-active={active || undefined}
              onClick={() => {
                setActiveId(itemId(item));
                onToggleMembers(item.members);
              }}
              className="flex flex-wrap items-center gap-x-3 gap-y-1 border-b border-border bg-surface-muted px-4 py-2 text-sm data-active:outline-2 data-active:-outline-offset-2 data-active:outline-focus"
            >
              <TriCheckbox
                state={state}
                tabIndex={-1}
                label={t("lemmas.bulk.review.originalAria", {
                  name: title,
                  total: original.count,
                  state: t(`lemmas.bulk.review.state.${state}`),
                })}
                onToggle={() => onToggleMembers(item.members)}
              />
              <Button
                variant="ghost"
                size="sm"
                tabIndex={-1}
                className="px-1 py-0"
                aria-label={t(
                  item.collapsed ? "lemmas.bulk.review.expand" : "lemmas.bulk.review.collapse",
                )}
                onClick={(event) => {
                  event.stopPropagation();
                  onToggleCollapse(item.key);
                }}
              >
                <span aria-hidden="true">{item.collapsed ? "▸" : "▾"}</span>
              </Button>
              <span className="font-medium">
                <span className="font-original">{title}</span>
              </span>
              {original.gloss && <span className="text-fg-muted">»{original.gloss}«</span>}
              {original.pos && <span className="text-fg-muted">{original.pos}</span>}
              {original.minority && (
                <span className="font-medium text-accent">
                  <span aria-hidden="true">! </span>
                  {t("lemmas.bulk.review.flagMinority")}
                </span>
              )}
              {original.posConflict && (
                <span className="font-medium text-accent">
                  <span aria-hidden="true">! </span>
                  {t("lemmas.bulk.review.flagPos")}
                </span>
              )}
              <small className="text-fg-muted">
                {t("lemmas.bulk.review.groupSummary", {
                  total: original.count,
                  selected: chosen,
                  onPage: item.onPage,
                })}
              </small>
            </div>
          );
        }
        if (item.kind === "group") {
          const { group } = item;
          const state = membersState(sel, group.members);
          const word = group.plainWord ?? "";
          const pos = group.partOfSpeech ?? "";
          const chosen = selectedInMembers(sel, group.members);
          return (
            <div
              key={virtual.key}
              {...common}
              id={`${listId}-${virtual.index}`}
              role="option"
              tabIndex={-1}
              aria-selected={state === "checked"}
              data-active={active || undefined}
              onClick={() => {
                setActiveId(itemId(item));
                onToggleMembers(group.members);
              }}
              className={`flex items-center gap-3 border-b border-border px-4 py-1.5 text-sm data-active:outline-2 data-active:-outline-offset-2 data-active:outline-focus ${
                origKeyOfGroupKey(item.key) ? "pl-10" : "bg-surface-muted py-2"
              }`}
            >
              <TriCheckbox
                state={state}
                tabIndex={-1}
                label={t("lemmas.bulk.review.groupAria", {
                  word,
                  pos,
                  total: group.count,
                  state: t(`lemmas.bulk.review.state.${state}`),
                })}
                onToggle={() => onToggleMembers(group.members)}
              />
              <span className="font-medium">{t("lemmas.bulk.review.group", { word, pos })}</span>
              <small className="text-fg-muted">
                {t("lemmas.bulk.review.groupSummary", {
                  total: group.count,
                  selected: chosen,
                  onPage: item.onPage,
                })}
              </small>
            </div>
          );
        }
        if (item.kind === "verse") {
          const verse = verses.get(item.row.wordId);
          return (
            <div
              key={virtual.key}
              {...common}
              role="presentation"
              className="border-b border-border bg-surface-muted px-4 py-2 pl-12 text-sm"
            >
              {verse?.status === "ready" ? (
                <p className="font-scripture">
                  <small className="mb-1 block font-sans text-fg-muted">
                    {abbreviation(item.row.bookId)} {item.row.chapter},{item.row.verse}
                  </small>
                  {verse.words.map((word) => (
                    <span key={word.wordId}>
                      {word.wordId === item.row.wordId ? (
                        <mark className="rounded-sm bg-mark-same text-fg">{word.text}</mark>
                      ) : (
                        word.text
                      )}{" "}
                    </span>
                  ))}
                </p>
              ) : verse?.status === "error" ? (
                <FormError message={t("lemmas.bulk.review.verseError")} />
              ) : (
                <p aria-busy="true" className="text-fg-muted">
                  {t("lemmas.bulk.review.verseLoading")}
                </p>
              )}
            </div>
          );
        }
        const { row } = item;
        const selectedRow = isSelected(sel, row.wordId, item.key);
        const staleInfo = stale.get(row.wordId);
        const isStale = staleInfo !== undefined;
        const kind = row.origKind;
        const open = expanded.has(row.wordId);
        const reference = `${abbreviation(row.bookId)} ${row.chapter},${row.verse}`;
        return (
          <div
            key={virtual.key}
            {...common}
            id={`${listId}-${virtual.index}`}
            role="option"
            tabIndex={-1}
            aria-selected={selectedRow}
            aria-posinset={item.index + 1}
            aria-setsize={rowCount}
            data-active={active || undefined}
            data-stale={isStale || undefined}
            onClick={(event) => {
              setActiveId(itemId(item));
              scrollRef.current?.focus({ preventScroll: true });
              onToggleRow(item.index, event.shiftKey);
            }}
            className={`bulk-row cursor-pointer border-b border-border px-4 py-1.5 text-sm hover:bg-surface-muted data-active:outline-2 data-active:-outline-offset-2 data-active:outline-focus data-stale:border-l-4 data-stale:border-l-accent ${
              selectedRow ? "" : "text-fg-muted"
            }`}
          >
            <input
              type="checkbox"
              tabIndex={-1}
              checked={selectedRow}
              readOnly
              aria-label={t("lemmas.bulk.review.select", { ref: reference })}
              onChange={() => {}}
            />
            <span className="whitespace-nowrap">{reference}</span>
            <span className="bulk-ctx truncate font-scripture">
              {row.left}{" "}
              <mark className="rounded-sm bg-mark-same text-fg">{row.wordInContext}</mark>{" "}
              {row.right}
            </span>
            <span className="bulk-orig flex items-center gap-1 whitespace-nowrap">
              {kind === "added" ? (
                <span className="text-fg-muted">{t("lemmas.bulk.review.rowAdded")}</span>
              ) : kind === "none" || !row.origKey ? (
                <span className="text-fg-muted">{t("lemmas.bulk.review.rowNone")}</span>
              ) : (
                <>
                  {row.posConflict && (
                    <span aria-hidden="true" className="font-medium text-accent">
                      !
                    </span>
                  )}
                  <span className="font-original">{row.origWord}</span>
                  <small className="text-fg-muted">{row.origStrongs ?? row.origKey}</small>
                </>
              )}
              <a
                href={`/bible/${row.bookId}/${row.chapter}#v${row.verse}`}
                target="_blank"
                rel="noopener noreferrer"
                tabIndex={-1}
                title={t("lemmas.bulk.review.mappingTitle")}
                aria-label={t("lemmas.bulk.review.mappingLink", { ref: reference })}
                className="rounded-sm px-1 text-accent underline"
                onClick={(event) => event.stopPropagation()}
              >
                {t("lemmas.bulk.review.mapping")}
              </a>
            </span>
            <span className="whitespace-nowrap">
              <s>{row.lemma}</s> → <b>{to}</b>
              {staleInfo && (
                <span className="ml-1 font-medium text-accent">
                  <span aria-hidden="true">! </span>
                  {staleInfo.reason === "original"
                    ? staleInfo.origKey
                      ? t("lemmas.bulk.review.staleOriginalNow", { value: staleInfo.origKey })
                      : t("lemmas.bulk.review.staleOriginal")
                    : staleInfo.lemma
                      ? t("lemmas.bulk.review.staleNow", { value: staleInfo.lemma })
                      : t("lemmas.bulk.review.stale")}
                </span>
              )}
            </span>
            <Button
              size="sm"
              tabIndex={-1}
              aria-expanded={open}
              disabled={disabled}
              onClick={(event) => {
                event.stopPropagation();
                onToggleVerse(row);
              }}
            >
              {open ? t("lemmas.bulk.review.hideVerse") : t("lemmas.bulk.review.showVerse")}
            </Button>
          </div>
        );
      })}
      <div aria-hidden="true" className="lemma-pad-bottom" />
    </div>
  );
}
