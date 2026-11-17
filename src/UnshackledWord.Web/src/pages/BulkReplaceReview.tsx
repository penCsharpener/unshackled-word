import { type KeyboardEvent, useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { Button } from "@/components/Button";
import { FormError } from "@/components/FormError";
import { MappingEditor, type MappingSaveError } from "@/components/MappingEditor";
import { TextField } from "@/components/TextField";
import { ApiError, describeApiError, describeApiErrors } from "@/lib/api";
import {
  applySuggestion,
  type QueueEntry,
  type QueueStatus,
  splitRef,
} from "@/lib/revision/bulkReplace";
import { reviewMarkers, toMappingData } from "@/lib/revision/mapping";
import {
  useRevisionLevels,
  useRevisionMapping,
  useRevisionVerse,
  useSaveRevision,
  useSaveRevisionMapping,
} from "@/lib/revision/queries";
import type { RevisionCarryReport } from "@/lib/revision/types";
import { wordDiff } from "@/lib/revision/wordDiff";

export interface BulkReplaceReviewProps {
  queue: readonly QueueEntry[];
  /** Replacement that prefills the text of every verse. */
  suggestion: string;
  word: string;
  /** Code of the revision the versions are saved to. */
  targetCode: string;
  referenceOf: (hebRefId: number) => string;
  onStatus: (hebRefId: number, status: QueueStatus) => void;
  /** Back to the result list. */
  onClose: () => void;
}

/** Steps 2 and 3 of the bulk replace: edit the verse text, then confirm the mapping, one verse at a time. */
export function BulkReplaceReview({
  queue,
  suggestion,
  word,
  targetCode,
  referenceOf,
  onStatus,
  onClose,
}: BulkReplaceReviewProps) {
  const { t } = useTranslation();
  const firstOpen = queue.findIndex((e) => e.status === "pending" || e.status === "saved");
  const [index, setIndex] = useState(firstOpen < 0 ? queue.length : firstOpen);
  const entry = queue[index];
  const total = queue.length;
  const next = () => setIndex((i) => i + 1);

  if (!entry) {
    const reviewed = queue.filter((e) => e.status === "reviewed").length;
    return (
      <section className="flex flex-col gap-3 rounded-md border border-border bg-surface p-4">
        <p role="status" className="font-medium">
          {t("bulkReplace.review.finished", { reviewed, total })}
        </p>
        <div>
          <Button onClick={onClose}>{t("bulkReplace.review.toList")}</Button>
        </div>
      </section>
    );
  }

  return (
    <ReviewVerse
      key={entry.hebRefId}
      entry={entry}
      position={index + 1}
      total={total}
      suggestion={suggestion}
      word={word}
      targetCode={targetCode}
      reference={referenceOf(entry.hebRefId)}
      onStatus={(status) => onStatus(entry.hebRefId, status)}
      onNext={next}
      onClose={onClose}
    />
  );
}

interface ReviewVerseProps {
  entry: QueueEntry;
  position: number;
  total: number;
  suggestion: string;
  word: string;
  targetCode: string;
  reference: string;
  onStatus: (status: QueueStatus) => void;
  onNext: () => void;
  onClose: () => void;
}

function ReviewVerse({
  entry,
  position,
  total,
  suggestion,
  word,
  targetCode,
  reference,
  onStatus,
  onNext,
  onClose,
}: ReviewVerseProps) {
  const { t } = useTranslation();
  const ref = splitRef(entry.hebRefId);
  const levels = useRevisionLevels();
  const verse = useRevisionVerse(ref);
  const save = useSaveRevision(ref);
  const saveMapping = useSaveRevisionMapping();
  const target = levels.data?.find((l) => l.code === targetCode && !l.isBase);
  const variant = verse.data?.variants.find((v) => v.level === target?.level);
  const current = variant?.text ?? verse.data?.baseText ?? "";
  const [draft, setDraft] = useState<string>();
  const [note, setNote] = useState<string>();
  const [carry, setCarry] = useState<RevisionCarryReport>();
  /** Set when the text of this verse is saved (now or in an earlier session). */
  const [savedId, setSavedId] = useState<number>();
  const [pendingEdits, setPendingEdits] = useState(0);
  const resumedId = entry.status === "saved" ? variant?.revisionVerseId : undefined;
  const mappingId = savedId ?? resumedId;
  const mapping = useRevisionMapping(mappingId ?? null);

  const suggested = applySuggestion(current, entry.forms, entry.occurrences, suggestion);
  const text = draft ?? suggested;
  const noteText = note ?? t("bulkReplace.review.defaultNote", { word, replacement: suggestion });
  const changed = text.trim() !== "" && text.trim() !== current.trim();
  const conflict = save.error instanceof ApiError && save.error.status === 409;
  const loading = levels.isLoading || verse.isLoading;
  const failed = levels.isError || verse.isError || (levels.isSuccess && !target);

  function submitText() {
    if (!target || !changed) return;
    save.mutate(
      {
        level: target.level,
        text: text.trim(),
        expectedVersion: variant?.version,
        note: noteText.trim() || undefined,
        basedOnLevel: variant ? target.level : 0,
      },
      {
        onSuccess: (response) => {
          setSavedId(response.revisionVerseId);
          setCarry(response.carry);
          onStatus("saved");
        },
      },
    );
  }

  function confirmMapping() {
    if (!mapping.data || mappingId === undefined) return;
    const links = mapping.data.links.map((l) => ({
      elbWordId: l.elbWordId,
      stepWordId: l.stepWordId,
      isAddedWord: l.isAddedWord,
      parentElbWordId: l.parentElbWordId,
    }));
    finishMapping(links);
  }

  function finishMapping(links: Parameters<typeof saveMapping.mutate>[0]["links"]) {
    if (mappingId === undefined) return;
    saveMapping.mutate(
      { id: mappingId, links },
      {
        onSuccess: () => {
          onStatus("reviewed");
          onNext();
        },
      },
    );
  }

  function mappingSaveError(): MappingSaveError | null {
    if (!saveMapping.isError) return null;
    const status = saveMapping.error instanceof ApiError ? saveMapping.error.status : 0;
    if (status === 400) return "validation";
    return status === 403 ? "forbidden" : "failed";
  }

  const inMapping = mappingId !== undefined;
  const root = useRef<HTMLElement>(null);
  const textReady = !inMapping && Boolean(verse.data && target);
  useEffect(() => {
    if (textReady) root.current?.querySelector("textarea")?.focus();
  }, [textReady]);

  /** Ctrl+Enter: continue to the mapping, or confirm an unedited mapping. */
  function onShortcut(event: KeyboardEvent<HTMLElement>) {
    if (event.key !== "Enter" || !(event.ctrlKey || event.metaKey)) return;
    event.preventDefault();
    if (!inMapping) submitText();
    else if (pendingEdits === 0) confirmMapping();
  }
  const parts = wordDiff(current, text);

  return (
    <section
      ref={root}
      onKeyDown={onShortcut}
      aria-label={t("bulkReplace.review.label", { reference })}
      className="flex flex-col gap-3 rounded-md border border-border bg-surface p-4"
    >
      <ol className="flex flex-wrap gap-2 text-sm" aria-label={t("bulkReplace.review.steps")}>
        <li className="rounded-full border border-border px-3 py-0.5 text-fg-muted">
          {t("bulkReplace.steps.hits")}
        </li>
        <li
          aria-current={inMapping ? undefined : "step"}
          className={`rounded-full border px-3 py-0.5 ${inMapping ? "border-border text-fg-muted" : "border-accent bg-accent text-accent-fg"}`}
        >
          {t("bulkReplace.steps.text")}
        </li>
        <li
          aria-current={inMapping ? "step" : undefined}
          className={`rounded-full border px-3 py-0.5 ${inMapping ? "border-accent bg-accent text-accent-fg" : "border-border text-fg-muted"}`}
        >
          {t("bulkReplace.steps.mapping")}
        </li>
      </ol>
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <h2 className="text-lg font-semibold">
          {reference}{" "}
          <span className="text-sm font-normal text-fg-muted">
            {t("bulkReplace.review.position", { position, total })}
          </span>
        </h2>
        <span className="text-sm text-fg-muted">
          {entry.strongs.length > 0 ? entry.strongs.join(", ") : t("bulkReplace.table.unlinked")}
        </span>
      </div>

      {loading ? (
        <p role="status" className="text-fg-muted">
          {t("bible.revision.loading")}
        </p>
      ) : null}
      {failed ? (
        <div role="alert" className="flex flex-wrap items-center gap-3">
          <p>{t("bulkReplace.review.loadError")}</p>
          <Button
            variant="secondary"
            size="sm"
            onClick={() => {
              void levels.refetch();
              void verse.refetch();
            }}
          >
            {t("bible.retry")}
          </Button>
        </div>
      ) : null}

      {!inMapping && verse.data && target ? (
        <>
          <p className="text-sm text-fg-muted">{t("bulkReplace.review.textHint")}</p>
          {entry.articleBefore ? (
            <p className="text-sm">
              ⚠ {t("bulkReplace.table.article", { article: entry.articleBefore })}
            </p>
          ) : null}
          <TextField
            label={t("bulkReplace.review.text", { target: target.name })}
            value={text}
            onChange={setDraft}
            multiline
            rows={4}
          />
          <p className="font-serif text-base">
            <span className="sr-only">{t("bulkReplace.review.diff")}: </span>
            {parts.map((part, i) => (
              // biome-ignore lint/suspicious/noArrayIndexKey: parts are rebuilt on every render and never reordered
              <span key={i}>
                {part.kind === "added" ? (
                  <ins className="rounded-sm bg-mark-linked no-underline">{part.text}</ins>
                ) : part.kind === "removed" ? (
                  <del className="text-fg-muted">{part.text}</del>
                ) : (
                  part.text
                )}{" "}
              </span>
            ))}
          </p>
          <TextField
            label={t("bulkReplace.review.note")}
            value={noteText}
            onChange={setNote}
            maxLength={500}
          />
          {conflict ? (
            <div role="alert" className="flex flex-wrap items-center gap-3">
              <p>{t("bulkReplace.review.conflict")}</p>
              <Button
                variant="secondary"
                size="sm"
                onClick={() => {
                  setDraft(undefined);
                  save.reset();
                  void verse.refetch();
                }}
              >
                {t("bulkReplace.review.reload")}
              </Button>
            </div>
          ) : save.isError ? (
            <FormError message={describeApiError(save.error, t("bulkReplace.review.saveFailed"))} />
          ) : null}
          <div className="flex flex-wrap gap-2">
            <Button variant="ghost" onClick={onClose}>
              {t("bulkReplace.review.toList")}
            </Button>
            <Button
              variant="secondary"
              onClick={() => {
                onStatus("skipped");
                onNext();
              }}
            >
              {t("bulkReplace.review.skip")}
            </Button>
            <Button onClick={submitText} disabled={!changed || save.isPending}>
              {t("bulkReplace.review.toMapping")}
            </Button>
          </div>
          {!changed ? (
            <p className="text-sm text-fg-muted">{t("bulkReplace.review.unchanged")}</p>
          ) : null}
        </>
      ) : null}

      {inMapping ? (
        <>
          <p className="text-sm text-fg-muted">
            {t("bulkReplace.review.mappingHint", { target: target?.name ?? targetCode })}
          </p>
          {carry ? (
            <p role="status" className="text-sm text-fg-muted">
              {t("bible.revision.carry", {
                same: carry.same,
                changed: carry.changed,
                new: carry.new,
                lost: carry.lostOriginalWordIds.length,
              })}
            </p>
          ) : null}
          {mapping.isLoading ? (
            <p role="status" className="text-fg-muted">
              {t("bible.mapping.loading")}
            </p>
          ) : null}
          {mapping.isError ? (
            <div role="alert" className="flex flex-wrap items-center gap-3">
              <p>{t("bible.mapping.loadError")}</p>
              <Button variant="secondary" size="sm" onClick={() => mapping.refetch()}>
                {t("bible.retry")}
              </Button>
            </div>
          ) : null}
          {mapping.data ? (
            <MappingEditor
              data={toMappingData(mapping.data, true)}
              reference={reference}
              review={reviewMarkers(mapping.data, carry)}
              saving={saveMapping.isPending}
              saveError={mappingSaveError()}
              saveErrorDetails={describeApiErrors(saveMapping.error)}
              onCancel={() => undefined}
              onChangeCount={setPendingEdits}
              onSave={finishMapping}
            />
          ) : null}
          <div className="flex flex-wrap gap-2">
            <Button variant="ghost" onClick={onClose}>
              {t("bulkReplace.review.toList")}
            </Button>
            <Button
              variant="secondary"
              onClick={() => {
                onNext();
              }}
            >
              {t("bulkReplace.review.later")}
            </Button>
            {pendingEdits === 0 ? (
              <Button onClick={confirmMapping} disabled={!mapping.data || saveMapping.isPending}>
                {t("bulkReplace.review.confirm")}
              </Button>
            ) : (
              <span className="self-center text-sm text-fg-muted">
                {t("bulkReplace.review.saveHint")}
              </span>
            )}
          </div>
        </>
      ) : null}
    </section>
  );
}
