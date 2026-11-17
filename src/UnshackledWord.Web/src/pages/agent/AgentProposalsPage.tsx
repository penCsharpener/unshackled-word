import { useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { Link, Navigate, useNavigate, useParams } from "react-router";
import { Button } from "@/components/Button";
import { Dialog } from "@/components/Dialog";
import { FormError } from "@/components/FormError";
import { Select } from "@/components/Select";
import { StatusBadge } from "@/components/StatusBadge";
import { formatRunRange } from "@/lib/agent/format";
import {
  useAgentProposals,
  useAgentRun,
  useAgentRuns,
  useBulkMergeProposals,
} from "@/lib/agent/queries";
import {
  bulkCounts,
  filterProposals,
  ignoresReviewKey,
  nextOpenProposal,
  type StatusFilter,
} from "@/lib/agent/review";
import {
  type AgentProposalSummary,
  type BulkMergeResponse,
  PROPOSAL_STATUSES,
  proposalTone,
  runTone,
} from "@/lib/agent/types";
import { describeApiErrors } from "@/lib/api";
import { useAuth } from "@/lib/auth/AuthContext";
import { hasWriteRole } from "@/lib/auth/roles";
import { bookName } from "@/lib/bible/books";
import { useBibleStructure } from "@/lib/bible/queries";
import { ProposalReview } from "./ProposalReview";

/** Names of the Bible books in the UI language, for references and run ranges. */
function useBookNames() {
  const { i18n } = useTranslation();
  const structure = useBibleStructure();
  return (id: number) => {
    const book = structure.data?.find((b) => b.id === id);
    return book ? bookName(book, i18n.language) : `#${id}`;
  };
}

/** Without a run: the runs that have proposals, each with a link to its review. */
function RunPicker() {
  const { t, i18n } = useTranslation();
  const runs = useAgentRuns();
  const name = useBookNames();
  const withProposals = (runs.data ?? []).filter((run) => run.proposedVerses > 0);
  return (
    <div className="flex flex-col gap-3">
      <p className="text-fg-muted">{t("agent.proposals.intro")}</p>
      {runs.isError ? <FormError message={t("agent.runs.loadFailed")} /> : null}
      {runs.isSuccess && withProposals.length === 0 ? (
        <p className="text-sm text-fg-muted">{t("agent.proposals.noRuns")}</p>
      ) : null}
      {withProposals.length > 0 ? (
        <ul className="flex flex-col gap-2">
          {withProposals.map((run) => (
            <li key={run.id}>
              <Link
                to={`/edit/proposals/${run.id}`}
                className="flex flex-wrap items-center gap-x-4 gap-y-1 rounded-md border border-border p-3 hover:bg-surface-muted"
              >
                <span className="font-medium tabular-nums">#{run.id}</span>
                <span>{formatRunRange(run, name(run.book))}</span>
                <span className="text-sm text-fg-muted">{run.model}</span>
                <span className="text-sm tabular-nums">
                  {t("agent.runs.proposalsCell", {
                    open: run.openProposals,
                    total: run.proposedVerses,
                  })}
                </span>
                {run.flaggedVerses > 0 ? (
                  <span className="text-sm text-fg-muted">
                    {t("agent.runs.flagged", { count: run.flaggedVerses })}
                  </span>
                ) : null}
                <span className="text-sm text-fg-muted" lang={i18n.language}>
                  {t(`agent.status.${run.status}`)}
                </span>
              </Link>
            </li>
          ))}
        </ul>
      ) : null}
    </div>
  );
}

/** Confirmation and result of "merge everything without flags". */
function BulkMergeDialog({
  runId,
  open,
  onClose,
  unflagged,
  flagged,
}: {
  runId: number;
  open: boolean;
  onClose: () => void;
  unflagged: number;
  flagged: number;
}) {
  const { t } = useTranslation();
  const bulk = useBulkMergeProposals();
  const [confirmed, setConfirmed] = useState(false);
  const [result, setResult] = useState<BulkMergeResponse | null>(null);
  const name = useBookNames();
  const run = useAgentRun(runId);

  function close() {
    if (bulk.isPending) return;
    setConfirmed(false);
    setResult(null);
    bulk.reset();
    onClose();
  }

  const errors = bulk.isError ? describeApiErrors(bulk.error) : [];
  return (
    <Dialog
      open={open}
      onClose={close}
      title={t("agent.proposals.bulk.title")}
      dismissible={!bulk.isPending}
      footer={
        <div className="flex flex-wrap justify-end gap-2">
          {result ? (
            <Button variant="primary" onClick={close}>
              {t("agent.proposals.bulk.close")}
            </Button>
          ) : (
            <>
              <Button variant="secondary" disabled={bulk.isPending} onClick={close}>
                {t("agent.proposals.bulk.cancel")}
              </Button>
              <Button
                variant="primary"
                disabled={!confirmed || unflagged === 0 || bulk.isPending}
                onClick={() => bulk.mutate(runId, { onSuccess: setResult })}
              >
                {bulk.isPending
                  ? t("agent.proposals.bulk.merging")
                  : t("agent.proposals.bulk.confirm", { count: unflagged })}
              </Button>
            </>
          )}
        </div>
      }
    >
      {result ? (
        <div className="flex flex-col gap-3 text-sm" role="status">
          <p>{t("agent.proposals.bulk.merged", { count: result.merged })}</p>
          <p className="text-fg-muted">
            {t("agent.proposals.bulk.skippedFlagged", { count: result.skippedFlagged })}
          </p>
          {result.failed.length > 0 ? (
            <div className="flex flex-col gap-1">
              <p className="font-medium">
                {t("agent.proposals.bulk.failed", { count: result.failed.length })}
              </p>
              <ul className="flex flex-col gap-1">
                {result.failed.map((failure) => (
                  <li key={failure.id}>
                    <Link
                      className="text-accent underline underline-offset-2"
                      to={`/edit/proposals/${runId}/${failure.id}`}
                      onClick={close}
                    >
                      {`${name(run.data?.book ?? 0)} ${failure.chapter},${failure.verse}`}
                    </Link>{" "}
                    <span className="text-fg-muted">
                      {t(`agent.proposals.bulk.reason.${failure.reason}`, {
                        defaultValue: failure.reason,
                      })}
                      {failure.messages.length > 0 ? `: ${failure.messages.join(" ")}` : ""}
                    </span>
                  </li>
                ))}
              </ul>
            </div>
          ) : null}
        </div>
      ) : (
        <div className="flex flex-col gap-3 text-sm">
          <p>{t("agent.proposals.bulk.intro", { count: unflagged })}</p>
          <p className="text-fg-muted">
            {t("agent.proposals.bulk.flaggedStay", { count: flagged })}
          </p>
          <label className="flex items-start gap-2">
            <input
              type="checkbox"
              className="mt-1"
              checked={confirmed}
              disabled={bulk.isPending || unflagged === 0}
              onChange={(event) => setConfirmed(event.target.checked)}
            />
            {t("agent.proposals.bulk.checkbox", { count: unflagged })}
          </label>
          {errors.map((message) => (
            <FormError key={message} message={message} />
          ))}
          {bulk.isError && errors.length === 0 ? (
            <FormError message={t("agent.proposals.bulk.error")} />
          ) : null}
        </div>
      )}
    </Dialog>
  );
}

const SHORTCUTS = [
  ["j / k, → / ←", "agent.proposals.keys.navigate"],
  ["m", "agent.proposals.keys.merge"],
  ["e", "agent.proposals.keys.edit"],
  ["d", "agent.proposals.keys.discard"],
  ["Esc", "agent.proposals.keys.escape"],
  ["?", "agent.proposals.keys.help"],
] as const;

function ShortcutHelp({ open, onClose }: { open: boolean; onClose: () => void }) {
  const { t } = useTranslation();
  return (
    <Dialog open={open} onClose={onClose} title={t("agent.proposals.keys.title")}>
      <dl className="flex flex-col gap-2 text-sm">
        {SHORTCUTS.map(([keys, label]) => (
          <div key={keys} className="flex gap-4">
            <dt className="w-32 shrink-0">
              <kbd className="rounded-sm border border-border px-1 text-xs">{keys}</kbd>
            </dt>
            <dd>{t(label)}</dd>
          </div>
        ))}
      </dl>
      <p className="mt-3 text-sm text-fg-muted">{t("agent.proposals.keys.inactive")}</p>
    </Dialog>
  );
}

function ProposalList({
  proposals,
  selectedId,
  runId,
  label,
  showStatus,
}: {
  proposals: AgentProposalSummary[];
  selectedId: number | null;
  runId: number;
  label: (proposal: AgentProposalSummary) => string;
  /** Off when the filter already shows a single status: the badge would repeat it on every row. */
  showStatus: boolean;
}) {
  const { t } = useTranslation();
  return (
    <ol aria-label={t("agent.proposals.list")} className="flex flex-col">
      {proposals.map((proposal) => (
        <li key={proposal.id}>
          <Link
            to={`/edit/proposals/${runId}/${proposal.id}`}
            aria-current={proposal.id === selectedId ? "true" : undefined}
            data-current={proposal.id === selectedId ? "" : undefined}
            className="flex flex-col gap-1 rounded-md px-2 py-1.5 text-sm hover:bg-surface-muted data-current:bg-mark-same"
          >
            <span className="flex items-center justify-between gap-2">
              <span className="font-medium tabular-nums">{label(proposal)}</span>
              {showStatus ? (
                <StatusBadge tone={proposalTone(proposal.status)}>
                  {t(`agent.proposals.status.${proposal.status}`)}
                </StatusBadge>
              ) : null}
            </span>
            {proposal.flagCount > 0 ||
            proposal.hasLemmaChanges ||
            proposal.hasRoleChanges ||
            proposal.hasParts ||
            proposal.hasNote ? (
              <span className="flex flex-wrap gap-x-3 gap-y-0.5 text-xs text-fg-muted">
                {proposal.flagCount > 0 ? (
                  <span className="font-medium text-fg">
                    {t("agent.proposals.flagCount", { count: proposal.flagCount })}
                  </span>
                ) : null}
                {proposal.hasLemmaChanges ? (
                  <span title={t("agent.proposals.markers.lemmas")}>
                    {t("agent.proposals.markerLabels.lemmas")}
                  </span>
                ) : null}
                {proposal.hasRoleChanges ? (
                  <span title={t("agent.proposals.markers.roles")}>
                    {t("agent.proposals.markerLabels.roles")}
                  </span>
                ) : null}
                {proposal.hasParts ? (
                  <span title={t("agent.proposals.markers.parts")}>
                    {t("agent.proposals.markerLabels.parts")}
                  </span>
                ) : null}
                {proposal.hasNote ? (
                  <span title={t("agent.proposals.markers.note")}>
                    {t("agent.proposals.markerLabels.note")}
                  </span>
                ) : null}
              </span>
            ) : null}
          </Link>
        </li>
      ))}
    </ol>
  );
}

function RunReview({ runId, proposalId }: { runId: number; proposalId: number | null }) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const name = useBookNames();
  const run = useAgentRun(runId);
  const proposals = useAgentProposals(runId);
  const [status, setStatus] = useState<StatusFilter>("open");
  const [flaggedOnly, setFlaggedOnly] = useState(false);
  const [rolesOnly, setRolesOnly] = useState(false);
  const [editing, setEditing] = useState(false);
  const [bulkOpen, setBulkOpen] = useState(false);
  const [helpOpen, setHelpOpen] = useState(false);
  const all = proposals.data ?? [];
  const shown = filterProposals(all, status, flaggedOnly, rolesOnly);
  /** The role filter is offered only for a run that has role changes (lemma mode). */
  const anyRoles = all.some((p) => p.hasRoleChanges);
  const selectedId = proposalId ?? shown[0]?.id ?? null;
  const index = shown.findIndex((p) => p.id === selectedId);
  const counts = bulkCounts(all);
  const verse = (p: AgentProposalSummary) => `${p.chapter},${p.verse}`;

  const go = (id: number | null) =>
    navigate(id === null ? `/edit/proposals/${runId}` : `/edit/proposals/${runId}/${id}`);
  const step = (delta: number) => {
    const target = shown[index + delta] ?? (index < 0 ? shown[0] : undefined);
    if (target) go(target.id);
  };

  // After a merge or discard: on to the next open proposal (read from the refreshed list through the ref).
  const latest = useRef({ all, selectedId, step, go });
  latest.current = { all, selectedId, step, go };
  const advance = () => {
    const { all: list, selectedId: current, go: to } = latest.current;
    const filtered = filterProposals(list, "all", flaggedOnly, rolesOnly);
    to(current === null ? null : nextOpenProposal(filtered, current));
  };

  useEffect(() => {
    function onKeyDown(event: KeyboardEvent) {
      if (editing || ignoresReviewKey(event)) return;
      const { step: move } = latest.current;
      if (event.key === "j" || event.key === "ArrowRight") move(1);
      else if (event.key === "k" || event.key === "ArrowLeft") move(-1);
      else if (event.key === "?") setHelpOpen(true);
      else return;
      event.preventDefault();
    }
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, [editing]);

  const current = run.data;
  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center gap-x-4 gap-y-2">
        <Link to="/edit/proposals" className="text-sm text-accent underline underline-offset-2">
          {t("agent.proposals.back")}
        </Link>
        {current ? (
          <>
            <span className="font-medium">
              {t("agent.proposals.runLabel", { id: current.id })} ·{" "}
              {formatRunRange(current, name(current.book))}
            </span>
            <span className="text-sm text-fg-muted">
              {current.profile} · {current.model}
            </span>
            <StatusBadge tone={runTone(current.status)}>
              {t(`agent.status.${current.status}`)}
            </StatusBadge>
          </>
        ) : null}
        <Button
          size="sm"
          className="ms-auto"
          aria-keyshortcuts="?"
          aria-label={t("agent.proposals.keys.open")}
          onClick={() => setHelpOpen(true)}
        >
          ?
        </Button>
      </div>

      <div className="flex flex-wrap items-end gap-x-4 gap-y-2">
        <Select
          label={t("agent.proposals.filter.status")}
          size="sm"
          value={status}
          onChange={(value) => setStatus(value as StatusFilter)}
          options={[
            { value: "all", label: t("agent.proposals.filter.all") },
            ...PROPOSAL_STATUSES.map((value) => ({
              value,
              label: t(`agent.proposals.status.${value}`),
            })),
          ]}
        />
        <label className="flex items-center gap-2 pb-1 text-sm">
          <input
            type="checkbox"
            checked={flaggedOnly}
            onChange={(event) => setFlaggedOnly(event.target.checked)}
          />
          {t("agent.proposals.filter.flaggedOnly")}
        </label>
        {anyRoles || rolesOnly ? (
          <label className="flex items-center gap-2 pb-1 text-sm">
            <input
              type="checkbox"
              checked={rolesOnly}
              onChange={(event) => setRolesOnly(event.target.checked)}
            />
            {t("agent.proposals.filter.rolesOnly")}
          </label>
        ) : null}
        <Button
          variant="primary"
          size="sm"
          className="ms-auto"
          disabled={counts.unflagged === 0}
          onClick={() => setBulkOpen(true)}
        >
          {t("agent.proposals.bulk.open", { count: counts.unflagged })}
        </Button>
      </div>

      {proposals.isError ? <FormError message={t("agent.proposals.loadListFailed")} /> : null}
      {proposals.isSuccess && shown.length === 0 && selectedId === null ? (
        <p className="text-sm text-fg-muted">
          {all.length === 0 ? t("agent.proposals.none") : t("agent.proposals.noneShown")}
        </p>
      ) : null}

      <div className="flex items-center gap-2 text-sm md:hidden">
        <Button size="sm" disabled={index <= 0} onClick={() => step(-1)}>
          {t("agent.proposals.previous")}
        </Button>
        <span className="tabular-nums" aria-live="polite">
          {index >= 0
            ? t("agent.proposals.position", { index: index + 1, total: shown.length })
            : t("agent.proposals.positionNone", { total: shown.length })}
        </span>
        <Button
          size="sm"
          disabled={index < 0 ? shown.length === 0 : index >= shown.length - 1}
          onClick={() => step(1)}
        >
          {t("agent.proposals.next")}
        </Button>
      </div>

      <div className="flex flex-col gap-4 md:flex-row">
        <div className="hidden max-h-screen overflow-y-auto md:block md:w-72 md:shrink-0">
          <ProposalList
            proposals={shown}
            selectedId={selectedId}
            runId={runId}
            label={verse}
            showStatus={status === "all"}
          />
        </div>
        <div className="min-w-0 md:flex-1">
          {selectedId !== null ? (
            <ProposalReview
              key={selectedId}
              proposalId={selectedId}
              onDone={advance}
              onEditingChange={setEditing}
            />
          ) : null}
        </div>
      </div>

      <BulkMergeDialog
        runId={runId}
        open={bulkOpen}
        onClose={() => setBulkOpen(false)}
        unflagged={counts.unflagged}
        flagged={counts.flagged}
      />
      <ShortcutHelp open={helpOpen} onClose={() => setHelpOpen(false)} />
    </div>
  );
}

/** Review of the proposals of agent runs (roadmap 0016.7f): list, compare, merge, edit, discard; keys j/k m e d. Maintainer only. */
export function AgentProposalsPage() {
  const { t } = useTranslation();
  const { status, user } = useAuth();
  const params = useParams();
  if (status === "loading") return null;
  if (status === "anonymous") return <Navigate to="/login" replace />;
  const runId = /^\d+$/.test(params.runId ?? "") ? Number(params.runId) : null;
  const proposalId = /^\d+$/.test(params.proposalId ?? "") ? Number(params.proposalId) : null;
  return (
    <section className="flex w-full flex-col gap-4 py-4">
      <h1 className="text-2xl font-semibold">{t("agent.proposals.title")}</h1>
      {!hasWriteRole(user) ? (
        <p role="alert" className="text-fg-muted">
          {t("admin.forbidden")}
        </p>
      ) : params.runId !== undefined && runId === null ? (
        <p role="alert" className="text-fg-muted">
          {t("agent.proposals.badRun")}
        </p>
      ) : runId !== null ? (
        <RunReview key={runId} runId={runId} proposalId={proposalId} />
      ) : (
        <RunPicker />
      )}
    </section>
  );
}
