import { useEffect, useMemo, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import { sumEstimates } from "@/lib/agent/estimate";
import { formatCost, formatTokens, skippedVersesFromNote } from "@/lib/agent/format";
import {
  useAgentEstimate,
  useAgentEstimates,
  useAgentProfiles,
  useAgentRun,
  useStartAgentRun,
} from "@/lib/agent/queries";
import {
  type AgentMode,
  type AgentRun,
  type AgentRunBlock,
  type AgentRunEstimate,
  type AgentRunRangeRequest,
  isRunActive,
  runTone,
} from "@/lib/agent/types";
import { describeApiErrors } from "@/lib/api";
import { useDebouncedValue } from "@/lib/useDebouncedValue";
import { Button, buttonClasses } from "./Button";
import { Dialog } from "./Dialog";
import { FormError } from "./FormError";
import { ProgressBar } from "./ProgressBar";
import { Select } from "./Select";
import { StatusBadge } from "./StatusBadge";
import { TextField } from "./TextField";

export interface AgentRunDialogProps {
  open: boolean;
  onClose: () => void;
  /** The verses to start with: a verse, a verse range or a whole chapter (leave `toVerse` out for the chapter end). */
  initial: {
    book: number;
    fromChapter: number;
    fromVerse: number;
    toChapter?: number;
    toVerse?: number;
  };
  /** Display name of the book, e.g. "Markus". */
  bookName: string;
  /** Called once with the run the server created. */
  onStarted?: (run: AgentRun) => void;
  /** Offered modes, the first preselected (default redo / fill-open / lemma); `/edit/lxx` passes `["lxx-tag"]`. */
  modes?: AgentMode[];
  /** Target of "Vorschläge prüfen" (default `/edit/proposals/{runId}`). */
  reviewHref?: (runId: number) => string;
  /**
   * Several contiguous verse blocks (`/edit/lxx` selection with gaps): the range fields are replaced by the read-only block list,
   * every block is estimated on its own (the dialog shows the sum) and started as its own run, one after the other.
   */
  blocks?: AgentRunBlock[];
  /** Mode `lxx-tag` in Daniel: only this text form is tagged (sent with every request). */
  textForm?: "og" | "th";
}

const ESTIMATE_DEBOUNCE_MS = 400;
const MODES: AgentMode[] = ["redo", "fill-open", "lemma"];

/** Positive whole number from a text field, else undefined. */
const toCount = (value: string): number | undefined =>
  /^\d+$/.test(value.trim()) && Number(value) > 0 ? Number(value) : undefined;

/**
 * "KI neu zuordnen": range, provider profile and mode of an agent run with a live estimate (packages, tokens, cost, cap), then the
 * progress of the started run. Starting only creates proposals; the run keeps going when the dialog is closed.
 */
export function AgentRunDialog(props: AgentRunDialogProps) {
  // The body mounts per opening, so every opening starts from `initial` with a fresh form.
  return props.open ? <AgentRunDialogBody {...props} /> : null;
}

function Stat({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex flex-col">
      <dt className="text-xs text-fg-muted">{label}</dt>
      <dd className="tabular-nums">{value}</dd>
    </div>
  );
}

function EstimateView({
  estimate,
  override,
  loading,
  parts,
}: {
  estimate: AgentRunEstimate;
  override: { maxTokens?: number; maxCostUsd?: number };
  loading: boolean;
  /** The estimates behind a sum (one per block run): an own cap applies per run, so each part is compared on its own. */
  parts?: AgentRunEstimate[];
}) {
  const { t, i18n } = useTranslation();
  const n = (value: number) => formatTokens(value, i18n.language);
  const maxTokens = override.maxTokens ?? estimate.maxTokens;
  const maxCost = override.maxCostUsd ?? estimate.maxCostUsd;
  const hasOverride = override.maxTokens !== undefined || override.maxCostUsd !== undefined;
  const overCap = (e: AgentRunEstimate) =>
    (override.maxTokens !== undefined && e.estimatedTotalTokens > override.maxTokens) ||
    (override.maxCostUsd !== undefined && e.estimatedCostUsd > override.maxCostUsd);
  const exceeds = hasOverride ? (parts ?? [estimate]).some(overCap) : estimate.exceedsCap;
  return (
    <section
      aria-label={t("agent.dialog.estimate.title")}
      aria-busy={loading}
      className="flex flex-col gap-3 rounded-md border border-border p-3"
    >
      <div className="flex items-center justify-between gap-2">
        <h3 className="font-medium">{t("agent.dialog.estimate.title")}</h3>
        {loading ? (
          <span role="status" className="text-sm text-fg-muted">
            {t("agent.dialog.estimate.updating")}
          </span>
        ) : null}
      </div>
      <dl className="grid grid-cols-2 gap-3 sm:grid-cols-4">
        <Stat label={t("agent.dialog.estimate.packages")} value={n(estimate.packages)} />
        <Stat
          label={t("agent.dialog.estimate.verses")}
          value={
            estimate.skippedVerses > 0
              ? t("agent.dialog.estimate.versesSkipped", {
                  count: estimate.verses,
                  skipped: estimate.skippedVerses,
                })
              : n(estimate.verses)
          }
        />
        <Stat label={t("agent.dialog.estimate.input")} value={n(estimate.estimatedInputTokens)} />
        <Stat
          label={t("agent.dialog.estimate.cached")}
          value={n(estimate.estimatedCachedInputTokens)}
        />
        <Stat label={t("agent.dialog.estimate.output")} value={n(estimate.estimatedOutputTokens)} />
        <Stat label={t("agent.dialog.estimate.total")} value={n(estimate.estimatedTotalTokens)} />
        <Stat
          label={t("agent.dialog.estimate.cost")}
          value={formatCost(estimate.estimatedCostUsd, i18n.language)}
        />
        <Stat
          label={t("agent.dialog.estimate.caps")}
          value={
            maxTokens === undefined && maxCost === undefined
              ? t("agent.dialog.estimate.noCap")
              : [
                  maxTokens === undefined
                    ? null
                    : t("agent.dialog.estimate.capTokens", { tokens: n(maxTokens) }),
                  maxCost === undefined ? null : formatCost(maxCost, i18n.language),
                ]
                  .filter(Boolean)
                  .join(" · ")
          }
        />
      </dl>
      <p className="text-xs text-fg-muted">{t("agent.dialog.estimate.margin")}</p>
      {exceeds ? (
        <p role="alert" className="flex items-start gap-2 text-sm font-medium">
          <StatusBadge tone="warning">{t("agent.dialog.estimate.exceedsBadge")}</StatusBadge>
          <span>{t("agent.dialog.estimate.exceeds")}</span>
        </p>
      ) : null}
      {estimate.warnings.length > 0 ? (
        <ul className="list-disc pl-5 text-sm text-fg-muted">
          {estimate.warnings.map((warning) => (
            <li key={warning}>{warning}</li>
          ))}
        </ul>
      ) : null}
    </section>
  );
}

function RunProgress({ runId, bookName }: { runId: number; bookName: string }) {
  const { t, i18n } = useTranslation();
  const run = useAgentRun(runId);
  if (!run.data) {
    return (
      <p role="status" className="text-fg-muted">
        {run.isError ? t("agent.dialog.progress.loadFailed") : t("agent.dialog.progress.loading")}
      </p>
    );
  }
  const r = run.data;
  const n = (value: number) => formatTokens(value, i18n.language);
  const finished = !isRunActive(r.status);
  const skipped = skippedVersesFromNote(r.statusNote);
  const barTone =
    r.status === "completed"
      ? "ok"
      : r.status === "failed"
        ? "error"
        : r.status === "budget-exceeded"
          ? "warning"
          : "info";
  return (
    <div className="flex flex-col gap-3">
      <p className="flex flex-wrap items-center gap-2">
        <span className="font-medium">{bookName}</span>
        <span role="status">
          <StatusBadge tone={runTone(r.status)}>{t(`agent.status.${r.status}`)}</StatusBadge>
        </span>
        <span className="text-sm text-fg-muted">
          {r.profile} · {r.model}
        </span>
      </p>
      <ProgressBar
        value={r.packagesCompleted + r.packagesFailed}
        max={r.packages}
        label={t("agent.dialog.progress.packages")}
        tone={barTone}
      />
      <dl className="grid grid-cols-2 gap-3 sm:grid-cols-4">
        <Stat label={t("agent.dialog.progress.input")} value={n(r.inputTokens)} />
        <Stat label={t("agent.dialog.progress.cached")} value={n(r.cachedInputTokens)} />
        <Stat label={t("agent.dialog.progress.output")} value={n(r.outputTokens)} />
        <Stat
          label={t("agent.dialog.progress.cost")}
          value={formatCost(r.costUsd, i18n.language)}
        />
        <Stat label={t("agent.dialog.progress.proposed")} value={n(r.proposedVerses)} />
        <Stat label={t("agent.dialog.progress.flagged")} value={n(r.flaggedVerses)} />
        {r.packagesFailed > 0 ? (
          <Stat label={t("agent.dialog.progress.failedPackages")} value={n(r.packagesFailed)} />
        ) : null}
        {skipped > 0 ? (
          <Stat label={t("agent.dialog.progress.skipped")} value={n(skipped)} />
        ) : null}
      </dl>
      {r.statusNote ? <p className="text-sm text-fg-muted">{r.statusNote}</p> : null}
      {finished && r.status === "budget-exceeded" ? (
        <p className="text-sm text-fg-muted">{t("agent.dialog.progress.budgetHint")}</p>
      ) : null}
    </div>
  );
}

function AgentRunDialogBody({
  onClose,
  initial,
  bookName,
  onStarted,
  modes = MODES,
  reviewHref = (id) => `/edit/proposals/${id}`,
  blocks,
  textForm,
}: AgentRunDialogProps) {
  const { t } = useTranslation();
  const profiles = useAgentProfiles();
  const start = useStartAgentRun();
  const [fromChapter, setFromChapter] = useState(String(initial.fromChapter));
  const [fromVerse, setFromVerse] = useState(String(initial.fromVerse));
  const [toChapter, setToChapter] = useState(initial.toChapter ? String(initial.toChapter) : "");
  const [toVerse, setToVerse] = useState(initial.toVerse ? String(initial.toVerse) : "");
  const [chosenProfile, setChosenProfile] = useState<string>();
  const [mode, setMode] = useState<AgentMode>(modes[0] ?? "redo");
  const [model, setModel] = useState("");
  const [maxTokens, setMaxTokens] = useState("");
  const [maxCost, setMaxCost] = useState("");
  const [runIds, setRunIds] = useState<number[]>([]);
  const [includeConfirmed, setIncludeConfirmed] = useState(false);
  const blockMode = blocks !== undefined && blocks.length > 0;
  const runId = runIds[runIds.length - 1];

  const profileList = profiles.data ?? [];
  const firstUsable = profileList.find((p) => p.usable)?.name;
  const profile = chosenProfile ?? firstUsable ?? "";
  const selected = profileList.find((p) => p.name === profile);

  const lxxOptions = useMemo(
    () =>
      mode === "lxx-tag"
        ? {
            ...(includeConfirmed ? { includeConfirmed: true } : {}),
            ...(textForm ? { textForm } : {}),
          }
        : {},
    [mode, includeConfirmed, textForm],
  );

  const shared = useMemo(
    () => ({
      profile,
      ...(model.trim() ? { model: model.trim() } : {}),
      mode,
      ...lxxOptions,
    }),
    [profile, model, mode, lxxOptions],
  );

  const request = useMemo<AgentRunRangeRequest | null>(() => {
    if (blockMode) return null;
    const fromC = toCount(fromChapter);
    const fromV = toCount(fromVerse);
    const toC = toCount(toChapter);
    const toV = toCount(toVerse);
    if (!fromC || !fromV || !selected?.usable) return null;
    if ((toChapter.trim() && !toC) || (toVerse.trim() && !toV)) return null;
    return {
      book: initial.book,
      fromChapter: fromC,
      fromVerse: fromV,
      ...(toC ? { toChapter: toC } : {}),
      ...(toV ? { toVerse: toV } : {}),
      ...shared,
    };
  }, [blockMode, initial.book, fromChapter, fromVerse, toChapter, toVerse, selected, shared]);

  const blockRequests = useMemo<AgentRunRangeRequest[] | null>(
    () =>
      blockMode && selected?.usable
        ? (blocks ?? []).map((b) => ({ book: initial.book, ...b, ...shared }))
        : null,
    [blockMode, blocks, selected, initial.book, shared],
  );

  const debounced = useDebouncedValue(request, ESTIMATE_DEBOUNCE_MS);
  const debouncedBlocks = useDebouncedValue(blockRequests, ESTIMATE_DEBOUNCE_MS);
  const settled = blockMode ? debouncedBlocks === blockRequests : debounced === request;
  const single = useAgentEstimate(debounced, true);
  const multi = useAgentEstimates(debouncedBlocks);
  const multiReady =
    debouncedBlocks !== null && multi.length > 0 && multi.every((q) => q.isSuccess);
  const multiData = multiReady ? multi.map((q) => q.data as AgentRunEstimate) : undefined;
  const multiError = multi.find((q) => q.isError)?.error;
  const estimate = blockMode
    ? {
        data: multiData ? sumEstimates(multiData) : undefined,
        isError: multiError !== undefined,
        error: multiError,
        isSuccess: multiReady,
        isFetching: multi.some((q) => q.isFetching),
        isPlaceholderData: false,
      }
    : single;
  const override = {
    maxTokens: toCount(maxTokens),
    maxCostUsd: Number(maxCost) > 0 ? Number(maxCost) : undefined,
  };
  const ready =
    (blockMode ? blockRequests !== null : request !== null) &&
    settled &&
    estimate.isSuccess &&
    !estimate.isPlaceholderData &&
    !start.isPending;

  function startRequest(body: AgentRunRangeRequest) {
    start.mutate(
      {
        ...body,
        ...(override.maxTokens ? { maxTokens: override.maxTokens } : {}),
        ...(override.maxCostUsd ? { maxCostUsd: override.maxCostUsd } : {}),
      },
      {
        onSuccess: (run) => {
          setRunIds((ids) => [...ids, run.id]);
          onStarted?.(run);
        },
      },
    );
  }

  function begin() {
    if (blockMode) {
      const first = blockRequests?.[0];
      if (first) startRequest(first);
    } else if (request) {
      startRequest(request);
    }
  }

  const progressRun = useAgentRun(runId ?? null);
  const lastStatus = progressRun.data?.status;
  const total = blockMode ? (blocks?.length ?? 1) : 1;
  // Blocks run one after the other: the next block starts when the previous run completed (a failed or capped run stops the chain).
  const nextBlock = runIds.length < total ? blockRequests?.[runIds.length] : undefined;
  const startRef = useRef(startRequest);
  startRef.current = startRequest;
  useEffect(() => {
    if (nextBlock && lastStatus === "completed" && !start.isPending && !start.isError) {
      startRef.current(nextBlock);
    }
  }, [nextBlock, lastStatus, start.isPending, start.isError]);
  const chainStopped =
    nextBlock !== undefined &&
    lastStatus !== undefined &&
    !isRunActive(lastStatus) &&
    lastStatus !== "completed";
  const finished =
    progressRun.data !== undefined &&
    !isRunActive(progressRun.data.status) &&
    (nextBlock === undefined || chainStopped);
  const startErrors = start.isError ? describeApiErrors(start.error) : [];
  const estimateErrors = estimate.isError ? describeApiErrors(estimate.error) : [];

  const footer =
    runId === undefined ? (
      <div className="flex justify-end gap-2">
        <Button variant="ghost" disabled={start.isPending} onClick={onClose}>
          {t("agent.dialog.cancel")}
        </Button>
        <Button variant="primary" disabled={!ready} onClick={begin}>
          {start.isPending ? t("agent.dialog.starting") : t("agent.dialog.start")}
        </Button>
      </div>
    ) : (
      <div className="flex flex-wrap justify-end gap-2">
        {(progressRun.data?.proposedVerses ?? 0) > 0 ? (
          <Link to={reviewHref(runId ?? 0)} className={buttonClasses({ variant: "secondary" })}>
            {t("agent.dialog.review")}
          </Link>
        ) : null}
        <Button variant="primary" onClick={onClose}>
          {finished ? t("agent.dialog.close") : t("agent.dialog.background")}
        </Button>
      </div>
    );

  const verseLabel = (chapter: string, verse: string) => `${chapter},${verse}`;
  return (
    <Dialog
      open
      onClose={onClose}
      dismissible={!start.isPending}
      size="lg"
      title={t("agent.dialog.title")}
      description={
        runId === undefined
          ? t("agent.dialog.intro")
          : `${bookName} ${verseLabel(fromChapter, fromVerse)}`
      }
      footer={footer}
    >
      {runId !== undefined ? (
        <div className="flex flex-col gap-4">
          {runIds.map((id) => (
            <RunProgress key={id} runId={id} bookName={bookName} />
          ))}
          {nextBlock && !chainStopped ? (
            <p role="status" className="text-sm text-fg-muted">
              {t("agent.dialog.blocks.waiting", { done: runIds.length, count: total })}
            </p>
          ) : null}
          {chainStopped ? (
            <p role="alert" className="text-sm text-fg-muted">
              {t("agent.dialog.blocks.stopped", { done: runIds.length, count: total })}
            </p>
          ) : null}
          {startErrors.map((message) => (
            <FormError key={message} message={message} />
          ))}
        </div>
      ) : (
        <form
          className="flex flex-col gap-4"
          onSubmit={(event) => {
            event.preventDefault();
            if (ready) begin();
          }}
        >
          <fieldset className="flex flex-col gap-2">
            <legend className="mb-1 font-medium">
              {t("agent.dialog.range")}: {bookName}
            </legend>
            {blockMode ? (
              <>
                <p className="text-sm text-fg-muted">
                  {t("agent.dialog.blocks.intro", { count: blocks?.length ?? 0 })}
                </p>
                <ul className="flex flex-col gap-1 text-sm">
                  {(blocks ?? []).map((b) => (
                    <li key={`${b.fromChapter}:${b.fromVerse}`} className="tabular-nums">
                      {b.fromChapter},{b.fromVerse}
                      {b.toChapter === b.fromChapter && b.toVerse === b.fromVerse
                        ? ""
                        : b.toChapter === b.fromChapter
                          ? `–${b.toVerse}`
                          : `–${b.toChapter},${b.toVerse}`}
                    </li>
                  ))}
                </ul>
              </>
            ) : (
              <div className="flex flex-wrap items-end gap-3">
                <TextField
                  label={t("agent.dialog.fromChapter")}
                  type="number"
                  min={1}
                  size="sm"
                  value={fromChapter}
                  onChange={setFromChapter}
                />
                <TextField
                  label={t("agent.dialog.fromVerse")}
                  type="number"
                  min={1}
                  size="sm"
                  value={fromVerse}
                  onChange={setFromVerse}
                />
                <TextField
                  label={t("agent.dialog.toChapter")}
                  type="number"
                  min={1}
                  size="sm"
                  required={false}
                  placeholder={fromChapter}
                  value={toChapter}
                  onChange={setToChapter}
                />
                <TextField
                  label={t("agent.dialog.toVerse")}
                  type="number"
                  min={1}
                  size="sm"
                  required={false}
                  placeholder={toChapter.trim() ? t("agent.dialog.chapterEnd") : fromVerse}
                  value={toVerse}
                  onChange={setToVerse}
                />
              </div>
            )}
          </fieldset>

          <div className="flex flex-col gap-2">
            <Select
              label={t("agent.dialog.profile")}
              value={profile}
              onChange={setChosenProfile}
              disabled={profileList.length === 0}
              options={profileList.map((p) => ({
                value: p.name,
                label: p.usable
                  ? `${p.name} · ${p.model}`
                  : `${p.name} · ${t("agent.dialog.unusable", { reason: p.unusableReason ?? t("agent.dialog.noReason") })}`,
                disabled: !p.usable,
                title: p.unusableReason,
              }))}
            />
            {profiles.isError ? <FormError message={t("agent.dialog.profilesFailed")} /> : null}
            {profiles.isSuccess && !firstUsable ? (
              <FormError message={t("agent.dialog.noUsableProfile")} />
            ) : null}
            <Select
              label={t("agent.dialog.mode")}
              value={mode}
              onChange={(value) => setMode(value as AgentMode)}
              options={modes.map((m) => ({ value: m, label: t(`agent.mode.${m}`) }))}
            />
            <p className="text-sm text-fg-muted">{t(`agent.dialog.modeHint.${mode}`)}</p>
            {mode === "lxx-tag" ? (
              <label className="flex items-center gap-2 text-sm">
                <input
                  type="checkbox"
                  checked={includeConfirmed}
                  onChange={(event) => setIncludeConfirmed(event.target.checked)}
                />
                {t("agent.dialog.includeConfirmed")}
              </label>
            ) : null}
          </div>

          <details className="rounded-md border border-border p-3">
            <summary className="cursor-pointer font-medium">{t("agent.dialog.advanced")}</summary>
            <div className="mt-3 flex flex-col gap-3">
              <TextField
                label={t("agent.dialog.model")}
                size="sm"
                required={false}
                placeholder={selected?.model}
                value={model}
                onChange={setModel}
              />
              <div className="flex flex-wrap items-end gap-3">
                <TextField
                  label={t("agent.dialog.maxTokens")}
                  type="number"
                  min={1}
                  size="sm"
                  required={false}
                  placeholder={
                    selected?.maxTokensPerRun ? String(selected.maxTokensPerRun) : undefined
                  }
                  value={maxTokens}
                  onChange={setMaxTokens}
                />
                <TextField
                  label={t("agent.dialog.maxCost")}
                  type="number"
                  min={0}
                  step="any"
                  size="sm"
                  required={false}
                  placeholder={selected?.maxCostPerRun ? String(selected.maxCostPerRun) : undefined}
                  value={maxCost}
                  onChange={setMaxCost}
                />
              </div>
              <p className="text-sm text-fg-muted">{t("agent.dialog.capsHint")}</p>
            </div>
          </details>

          {estimate.data ? (
            <EstimateView
              estimate={estimate.data}
              override={override}
              loading={!ready && (estimate.isFetching || !settled)}
              parts={blockMode ? multiData : undefined}
            />
          ) : request !== null || blockRequests !== null ? (
            <p role="status" className="text-fg-muted">
              {t("agent.dialog.estimate.loading")}
            </p>
          ) : (
            <p className="text-fg-muted">{t("agent.dialog.estimate.needRange")}</p>
          )}
          {estimateErrors.map((message) => (
            <FormError key={message} message={message} />
          ))}
          {estimate.isError && estimateErrors.length === 0 ? (
            <FormError message={t("agent.dialog.estimate.failed")} />
          ) : null}
          {startErrors.map((message) => (
            <FormError key={message} message={message} />
          ))}
          {start.isError && startErrors.length === 0 ? (
            <FormError message={t("agent.dialog.startFailed")} />
          ) : null}
        </form>
      )}
    </Dialog>
  );
}
