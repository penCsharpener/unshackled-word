import { useEffect, useMemo, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { Navigate } from "react-router";
import { BulkReplaceTable, rowKey } from "@/components/BulkReplaceTable";
import { Button } from "@/components/Button";
import { EmptyState } from "@/components/EmptyState";
import { FormError } from "@/components/FormError";
import { SaveBar } from "@/components/SaveBar";
import { Select } from "@/components/Select";
import { TextField } from "@/components/TextField";
import { ToggleMenu } from "@/components/ToggleMenu";
import { describeApiError } from "@/lib/api";
import { useAuth } from "@/lib/auth/AuthContext";
import { hasWriteRole } from "@/lib/auth/roles";
import { bookAbbreviation } from "@/lib/bible/books";
import { useBibleStructure } from "@/lib/bible/queries";
import {
  type BulkReplaceFilter,
  buildQueue,
  isQuick,
  openCount,
  type QueueEntry,
  type QueueStatus,
  readStored,
  splitRef,
  writeStored,
} from "@/lib/revision/bulkReplace";
import { useBulkReplaceSearch, useRevisionLevels } from "@/lib/revision/queries";
import type { BulkReplaceRow, BulkReplaceSearchResponse } from "@/lib/revision/types";
import { BulkReplaceReview } from "./BulkReplaceReview";

const DEFAULT_CODE = "elb2027";
const NO_STRONGS = "__none__";
const WORD_PATTERN = /^[\p{L}\-']{2,40}$/u;

/** Bulk word replace (roadmap 0013): find a word, pick the verses, then review text and mapping verse by verse. */
export function BulkReplacePage() {
  const { t, i18n } = useTranslation();
  const { status, user } = useAuth();
  const levels = useRevisionLevels();
  const structure = useBibleStructure();
  const search = useBulkReplaceSearch();
  const stored = useRef(readStored()).current;

  const [filter, setFilter] = useState<BulkReplaceFilter>(
    stored?.filter ?? {
      word: "",
      sameLemma: false,
      source: DEFAULT_CODE,
      target: DEFAULT_CODE,
      suggestion: "",
    },
  );
  const [result, setResult] = useState<BulkReplaceSearchResponse>();
  const [checked, setChecked] = useState<Set<string>>(new Set());
  const [strongsOff, setStrongsOff] = useState<Set<string>>(new Set());
  const [queue, setQueue] = useState<QueueEntry[]>(stored?.queue ?? []);
  const [reviewing, setReviewing] = useState(false);

  useEffect(() => {
    writeStored(queue.length > 0 ? { filter, queue } : undefined);
  }, [filter, queue]);

  const revisions = levels.data ?? [];
  const sourceOptions = revisions.map((r) => ({ value: r.code, label: r.name }));
  const targetOptions = revisions
    .filter((r) => !r.isBase)
    .map((r) => ({ value: r.code, label: r.name }));
  const wordValid = WORD_PATTERN.test(filter.word.trim());
  const set = (patch: Partial<BulkReplaceFilter>) => setFilter((f) => ({ ...f, ...patch }));

  const bookOf = (id: number) => structure.data?.find((b) => b.id === id);
  const referenceOf = (hebRefId: number) => {
    const { bookId, chapter, verse } = splitRef(hebRefId);
    const book = bookOf(bookId);
    return `${book ? bookAbbreviation(book, i18n.language) : bookId} ${chapter},${verse}`;
  };
  const pathOf = (row: BulkReplaceRow) => {
    const { bookId, chapter, verse } = splitRef(row.hebRefId);
    return `/bible/${bookId}/${chapter}#v${verse}`;
  };

  const rows = result?.rows ?? [];
  const visible = useMemo(
    () =>
      rows.filter((r) =>
        r.strongs.length === 0
          ? !strongsOff.has(NO_STRONGS)
          : r.strongs.some((s) => !strongsOff.has(s)),
      ),
    [rows, strongsOff],
  );
  const checkedVisible = visible.filter((r) => checked.has(rowKey(r)));
  const verseCount = new Set(checkedVisible.map((r) => r.hebRefId)).size;
  const reviewed = useMemo(
    () => new Set(queue.filter((e) => e.status === "reviewed").map((e) => e.hebRefId)),
    [queue],
  );

  if (status === "loading") return null;
  if (status === "anonymous") return <Navigate to="/login" replace />;
  if (!hasWriteRole(user)) {
    return (
      <p role="alert" className="mx-auto max-w-4xl p-4">
        {t("admin.forbidden")}
      </p>
    );
  }

  function runSearch() {
    if (!wordValid) return;
    search.mutate(
      {
        word: filter.word.trim(),
        sameLemma: filter.sameLemma,
        sourceRevision: filter.source,
        targetRevision: filter.target,
      },
      {
        onSuccess: (response) => {
          setResult(response);
          setChecked(new Set(response.rows.map(rowKey)));
          setStrongsOff(new Set());
        },
      },
    );
  }

  function startReview() {
    setQueue(buildQueue(checkedVisible));
    setReviewing(true);
  }

  function setStatus(hebRefId: number, next: QueueStatus) {
    setQueue((q) => q.map((e) => (e.hebRefId === hebRefId ? { ...e, status: next } : e)));
  }

  const facetItems = (result?.facets ?? []).map((f) => ({
    key: f.strongs ?? NO_STRONGS,
    label: `${f.strongs ?? t("bulkReplace.table.unlinked")} · ${f.count}`,
    checked: !strongsOff.has(f.strongs ?? NO_STRONGS),
  }));

  const open = openCount(queue);

  return (
    <section className="mx-auto flex w-full max-w-6xl flex-col gap-4 p-4 pb-24">
      <h1 className="text-2xl font-semibold">{t("bulkReplace.title")}</h1>

      {reviewing ? (
        <BulkReplaceReview
          queue={queue}
          suggestion={filter.suggestion}
          word={filter.word}
          targetCode={filter.target}
          referenceOf={referenceOf}
          onStatus={setStatus}
          onClose={() => setReviewing(false)}
        />
      ) : (
        <>
          <p className="text-fg-muted">{t("bulkReplace.intro")}</p>
          {open > 0 ? (
            <div
              role="status"
              className="flex flex-wrap items-center gap-3 rounded-md border border-border bg-surface-muted p-3"
            >
              <span>{t("bulkReplace.resume.open", { count: open })}</span>
              <Button size="sm" onClick={() => setReviewing(true)}>
                {t("bulkReplace.resume.continue")}
              </Button>
              <Button
                size="sm"
                variant="ghost"
                onClick={() => {
                  setQueue([]);
                }}
              >
                {t("bulkReplace.resume.discard")}
              </Button>
            </div>
          ) : null}

          <form
            className="flex flex-col gap-3 rounded-md border border-border bg-surface p-4"
            onSubmit={(event) => {
              event.preventDefault();
              runSearch();
            }}
          >
            <div className="flex flex-wrap items-end gap-3">
              <Select
                label={t("bulkReplace.form.source")}
                value={filter.source}
                onChange={(value) => set({ source: value })}
                options={sourceOptions}
              />
              <Select
                label={t("bulkReplace.form.target")}
                value={filter.target}
                onChange={(value) => set({ target: value })}
                options={targetOptions}
              />
              <p className="max-w-sm text-sm text-fg-muted">{t("bulkReplace.form.sourceHint")}</p>
            </div>
            <div className="flex flex-wrap items-end gap-3">
              <TextField
                label={t("bulkReplace.form.word")}
                type="search"
                value={filter.word}
                onChange={(value) => set({ word: value })}
                invalid={filter.word !== "" && !wordValid}
                maxLength={40}
              />
              <label className="flex items-center gap-2 pb-2 text-sm">
                <input
                  type="checkbox"
                  checked={filter.sameLemma}
                  onChange={(event) => set({ sameLemma: event.target.checked })}
                  className="size-4 accent-accent"
                />
                {t("bulkReplace.form.sameLemma")}
              </label>
              <TextField
                label={t("bulkReplace.form.suggestion")}
                value={filter.suggestion}
                onChange={(value) => set({ suggestion: value })}
                maxLength={60}
              />
              <Button type="submit" disabled={!wordValid || search.isPending}>
                {t("bulkReplace.form.search")}
              </Button>
            </div>
            {filter.word !== "" && !wordValid ? (
              <FormError message={t("bulkReplace.form.invalidWord")} />
            ) : null}
            {search.isError ? (
              <FormError message={describeApiError(search.error, t("bulkReplace.form.failed"))} />
            ) : null}
          </form>

          {!result && !search.isPending ? <EmptyState>{t("bulkReplace.idle")}</EmptyState> : null}
          {search.isPending ? (
            <p role="status" aria-busy="true" className="text-fg-muted">
              {t("bulkReplace.searching")}
            </p>
          ) : null}

          {result && !search.isPending && result.rows.length === 0 ? (
            <EmptyState>{t("bulkReplace.empty")}</EmptyState>
          ) : null}
          {result && !search.isPending && result.rows.length > 0 ? (
            <>
              <div className="flex flex-wrap items-center gap-2">
                <p role="status" className="mr-auto font-medium">
                  {t("bulkReplace.count", {
                    total: visible.length,
                    selected: checkedVisible.length,
                  })}
                </p>
                {result.lemmas.length > 0 ? (
                  <span className="text-sm text-fg-muted">
                    {t("bulkReplace.lemmas", { lemmas: result.lemmas.join(", ") })}
                  </span>
                ) : null}
                <ToggleMenu
                  label={t("bulkReplace.facets.label", {
                    shown: facetItems.filter((f) => f.checked).length,
                    total: facetItems.length,
                  })}
                  items={facetItems}
                  onChange={(key, on) =>
                    setStrongsOff((prev) => {
                      const next = new Set(prev);
                      if (on) next.delete(key);
                      else next.add(key);
                      return next;
                    })
                  }
                  onReset={() => setStrongsOff(new Set())}
                  resetLabel={t("bulkReplace.facets.all")}
                />
                <Button
                  size="sm"
                  variant="secondary"
                  onClick={() => setChecked(new Set(visible.map(rowKey)))}
                >
                  {t("bulkReplace.toolbar.all")}
                </Button>
                <Button size="sm" variant="secondary" onClick={() => setChecked(new Set())}>
                  {t("bulkReplace.toolbar.none")}
                </Button>
                <Button
                  size="sm"
                  variant="secondary"
                  onClick={() =>
                    setChecked(new Set(visible.filter((r) => !checked.has(rowKey(r))).map(rowKey)))
                  }
                >
                  {t("bulkReplace.toolbar.invert")}
                </Button>
                <Button
                  size="sm"
                  variant="secondary"
                  onClick={() => setChecked(new Set(visible.filter(isQuick).map(rowKey)))}
                >
                  {t("bulkReplace.toolbar.quick")}
                </Button>
              </div>
              {result.capped ? (
                <p role="status" className="text-sm">
                  {t("bulkReplace.capped")}
                </p>
              ) : null}
              <BulkReplaceTable
                rows={visible}
                checked={checked}
                onToggle={(key, on) =>
                  setChecked((prev) => {
                    const next = new Set(prev);
                    if (on) next.add(key);
                    else next.delete(key);
                    return next;
                  })
                }
                referenceOf={(row) => referenceOf(row.hebRefId)}
                pathOf={pathOf}
                reviewed={reviewed}
                label={t("bulkReplace.table.label")}
              />
              <SaveBar
                count={verseCount}
                status="idle"
                hint={t("bulkReplace.bar.hint", {
                  target: revisions.find((r) => r.code === filter.target)?.name ?? filter.target,
                })}
                saveLabel={t("bulkReplace.bar.review", { count: verseCount })}
                disabled={verseCount === 0 || filter.suggestion.trim() === ""}
                message={
                  filter.suggestion.trim() === ""
                    ? t("bulkReplace.bar.suggestionMissing")
                    : undefined
                }
                onSave={startReview}
                onDiscard={() => setChecked(new Set(visible.map(rowKey)))}
              />
            </>
          ) : null}
        </>
      )}
    </section>
  );
}
