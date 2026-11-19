import { useQueryClient } from "@tanstack/react-query";
import { useEffect, useMemo, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { Button } from "@/components/Button";
import { Dialog } from "@/components/Dialog";
import { FormError } from "@/components/FormError";
import { MappingEditor } from "@/components/MappingEditor";
import { Select } from "@/components/Select";
import { StatusBadge } from "@/components/StatusBadge";
import { TextField } from "@/components/TextField";
import {
  agentKeys,
  useAgentProposal,
  useDiscardProposal,
  useEditMergeProposal,
  useMergeProposal,
  useRefreshAfterReview,
} from "@/lib/agent/queries";
import { ignoresReviewKey, liveLinks, roleChangeParts } from "@/lib/agent/review";
import {
  type AgentLemmaDiff,
  type AgentProposalDiff,
  type AgentRoleDiff,
  type AgentVerseLemma,
  proposalTone,
} from "@/lib/agent/types";
import { ApiError, describeApiErrors } from "@/lib/api";
import { bookName } from "@/lib/bible/books";
import { useBibleStructure } from "@/lib/bible/queries";
import type {
  GetVerseMappingResponse,
  VerseMappingLink,
  VerseMappingOriginalWord,
} from "@/lib/bible/types";
import { formatStamp } from "@/lib/format";
import { buildLabels } from "@/lib/lemmas/labels";
import { useLemmaValueMaps } from "@/lib/lemmas/queries";
import { buildStts } from "@/lib/lemmas/stts";

/** Messages of a failed review call: 409 (not open / stale), 400 (every hard-rule message), 403 (editing switched off). */
export function reviewProblems(error: unknown, t: (key: string) => string): string[] {
  const messages = describeApiErrors(error);
  if (error instanceof ApiError) {
    if (error.status === 403) return [t("agent.proposals.error.forbidden")];
    if (error.status === 404) return [t("agent.proposals.error.notFound")];
    if (error.status === 409) return [t("agent.proposals.error.conflict"), ...messages];
    if (error.status === 400 && messages.length > 0) return messages;
  }
  return messages.length > 0 ? messages : [t("agent.proposals.error.failed")];
}

interface LemmaEdit {
  lemma: string;
  stts: string;
  skip: boolean;
}

const Kbd = ({ children }: { children: string }) => (
  <kbd className="rounded-sm border border-border px-1 text-xs font-normal text-fg-muted">
    {children}
  </kbd>
);

/** Lemma changes of a proposal: read-only (compare) or editable per row (edit-merge). */
function LemmaTable({
  changes,
  edits,
  onEdit,
}: {
  changes: AgentLemmaDiff[];
  /** Present while the proposal is edited. */
  edits?: Record<number, LemmaEdit>;
  onEdit?: (id: number, edit: LemmaEdit) => void;
}) {
  const { t, i18n } = useTranslation();
  const maps = useLemmaValueMaps(edits !== undefined);
  const labels = useMemo(() => buildLabels(maps.data, i18n.language), [maps.data, i18n.language]);
  const stts = useMemo(() => buildStts(maps.data, labels), [maps.data, labels]);
  if (changes.length === 0) return null;
  const columns = ["word", "old", "new", "stts"];
  return (
    <div className="flex flex-col gap-2">
      <h3 className="text-sm font-semibold">{t("agent.proposals.lemmas.title")}</h3>
      <div className="overflow-x-auto">
        <table className="w-full text-left text-sm">
          <caption className="sr-only">{t("agent.proposals.lemmas.title")}</caption>
          <thead className="text-fg-muted">
            <tr className="border-b border-border">
              {columns.map((column) => (
                <th key={column} scope="col" className="px-2 py-1 font-medium">
                  {t(`agent.proposals.lemmas.${column}`)}
                </th>
              ))}
              {edits ? <th scope="col" className="px-2 py-1" /> : null}
            </tr>
          </thead>
          <tbody>
            {changes.map((change) => {
              const edit = edits?.[change.elbWordId];
              const options = stts.options(change.partOfSpeech ?? null, edit?.stts ?? null, false);
              return (
                <tr
                  key={change.elbWordId}
                  className={`border-b border-border align-top ${edit?.skip ? "opacity-50" : ""}`}
                >
                  <th scope="row" className="whitespace-nowrap px-2 py-1 font-medium" lang="de">
                    {change.german}
                  </th>
                  <td className="px-2 py-1">
                    {change.currentLemma ?? "–"}
                    {change.currentStts ? (
                      <span className="text-fg-muted"> · {change.currentStts}</span>
                    ) : null}
                    {change.changed ? (
                      <p className="mt-1">
                        <StatusBadge tone="warning">
                          {t("agent.proposals.lemmas.changedLive")}
                        </StatusBadge>
                        <span className="block text-xs text-fg-muted">
                          {t("agent.proposals.lemmas.changedHint", {
                            before: change.beforeLemma ?? "–",
                          })}
                        </span>
                      </p>
                    ) : null}
                  </td>
                  <td className="px-2 py-1">
                    {edit && onEdit ? (
                      <TextField
                        label={t("agent.proposals.lemmas.newFor", { word: change.german })}
                        labelHidden
                        size="sm"
                        required={false}
                        disabled={edit.skip}
                        value={edit.lemma}
                        onChange={(lemma) => onEdit(change.elbWordId, { ...edit, lemma })}
                      />
                    ) : (
                      <strong>{change.lemma}</strong>
                    )}
                  </td>
                  <td className="px-2 py-1">
                    {edit && onEdit ? (
                      <Select
                        label={t("agent.proposals.lemmas.sttsFor", { word: change.german })}
                        labelHidden
                        size="sm"
                        disabled={edit.skip}
                        value={edit.stts}
                        onChange={(value) => onEdit(change.elbWordId, { ...edit, stts: value })}
                        options={[{ value: "", label: "–" }, ...options]}
                      />
                    ) : (
                      (change.stts ?? "–")
                    )}
                  </td>
                  {edit && onEdit ? (
                    <td className="px-2 py-1">
                      <Button
                        size="sm"
                        aria-pressed={edit.skip}
                        onClick={() => onEdit(change.elbWordId, { ...edit, skip: !edit.skip })}
                      >
                        {t("agent.proposals.lemmas.skip")}
                      </Button>
                    </td>
                  ) : null}
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
  );
}

/** All words of the verse with their live lemma; the ones the proposal changes are highlighted and show the new lemma. */
function VerseLemmaTable({
  words,
  changes,
}: {
  words: AgentVerseLemma[];
  changes: AgentLemmaDiff[];
}) {
  const { t } = useTranslation();
  const proposed = useMemo(() => new Map(changes.map((c) => [c.elbWordId, c])), [changes]);
  if (words.length === 0) return null;
  return (
    <div className="flex flex-col gap-2">
      <h3 className="text-sm font-semibold">{t("agent.proposals.lemmas.verseTitle")}</h3>
      <p className="text-xs text-fg-muted">{t("agent.proposals.lemmas.verseHint")}</p>
      <div className="overflow-x-auto">
        <table className="w-full text-left text-sm">
          <caption className="sr-only">{t("agent.proposals.lemmas.verseTitle")}</caption>
          <thead className="text-fg-muted">
            <tr className="border-b border-border">
              {["word", "old", "stts"].map((column) => (
                <th key={column} scope="col" className="px-2 py-1 font-medium">
                  {t(`agent.proposals.lemmas.${column}`)}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {words.map((word) => {
              const change = word.proposed ? proposed.get(word.elbWordId) : undefined;
              return (
                <tr
                  key={word.elbWordId}
                  data-proposed={word.proposed ? "true" : undefined}
                  className={`border-b border-border align-top ${word.proposed ? "border-s-2 border-s-sev-warning bg-surface" : ""}`}
                >
                  <th scope="row" className="whitespace-nowrap px-2 py-1 font-medium" lang="de">
                    {word.german}
                  </th>
                  <td className="px-2 py-1">
                    {word.lemma ?? "–"}
                    {change ? (
                      <span className="ms-2">
                        <strong>{t("agent.proposals.lemmas.proposedTo", { lemma: change.lemma })}</strong>{" "}
                        <StatusBadge tone="warning">{t("agent.proposals.lemmas.proposed")}</StatusBadge>
                      </span>
                    ) : null}
                  </td>
                  <td className="px-2 py-1">
                    {word.stts ?? "–"}
                    {change?.stts && change.stts !== word.stts ? (
                      <strong> → {change.stts}</strong>
                    ) : null}
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
  );
}

/** `H9001 ו and`: a morpheme of the linked Hebrew word with its text and gloss; no number = the main morpheme. */
function MorphemeLabel({ strongs, word }: { strongs?: string; word?: VerseMappingOriginalWord }) {
  const { t } = useTranslation();
  const morphemes = word?.morphemes ?? [];
  const morpheme = strongs
    ? morphemes.find((m) => m.strongs === strongs)
    : morphemes.find((m) => m.isMain);
  const number = strongs ?? morpheme?.strongs;
  if (!number) return null;
  return (
    <span className="whitespace-nowrap">
      <span className="font-mono text-xs">{number}</span>
      {morpheme?.original ? (
        <span className="ms-1 original-text" lang="he" dir="rtl">
          {morpheme.original}
        </span>
      ) : null}
      {morpheme?.gloss ? <span className="ms-1 text-fg-muted">{morpheme.gloss}</span> : null}
      {!strongs ? (
        <span className="ms-1 text-xs text-fg-muted">({t("agent.proposals.roles.main")})</span>
      ) : null}
    </span>
  );
}

/** One side (before / proposed) of a role change: role, morpheme, multi-head and part lemma, or the added word. */
function RoleSide({
  change,
  side,
  word,
}: {
  change: AgentRoleDiff;
  side: "before" | "new";
  word?: VerseMappingOriginalWord;
}) {
  const { t } = useTranslation();
  const parts = roleChangeParts(change);
  const before = side === "before";
  if (!before && parts.added) {
    return (
      <strong>
        {change.addedParentGerman
          ? t("agent.proposals.roles.added", { parent: change.addedParentGerman })
          : t("agent.proposals.roles.addedNoParent")}
      </strong>
    );
  }
  const role = (before ? change.beforeRole : change.role) ?? "head";
  const strongs = before ? change.beforeStrongsNumber : change.strongsNumber;
  const multiHead = before ? change.beforeMultiHead : change.multiHead;
  const partLemma = before ? change.beforePartLemma : change.partLemma;
  const hebrew = (word?.morphemes?.length ?? 0) > 0;
  const mark = (changed: boolean) => (!before && changed ? "font-semibold" : "");
  return (
    <span className="flex flex-wrap items-baseline gap-x-2 gap-y-0.5">
      <span className={mark(parts.role || parts.added)}>
        {t(`agent.proposals.roles.${role === "unit" ? "unit" : "head"}`)}
      </span>
      {hebrew && (parts.morpheme || parts.added || strongs) ? (
        <span className={mark(parts.morpheme)}>
          <MorphemeLabel strongs={strongs} word={word} />
        </span>
      ) : null}
      {multiHead ? (
        <span className={`text-xs ${mark(parts.multiHead)}`}>
          {t("agent.proposals.roles.multiHead")}
        </span>
      ) : null}
      {partLemma ? (
        <span className={`text-xs ${mark(parts.partLemma)}`} lang="de">
          {t("agent.proposals.roles.partLemma", { lemma: partLemma })}
        </span>
      ) : null}
    </span>
  );
}

/** Role and morpheme changes of a lemma-mode proposal (head -> unit, H0559 -> H9001, pronoun -> added word), read-only. */
function RoleTable({
  changes,
  originalWords,
}: {
  changes: AgentRoleDiff[];
  originalWords: VerseMappingOriginalWord[];
}) {
  const { t } = useTranslation();
  const byId = useMemo(() => new Map(originalWords.map((w) => [w.id, w])), [originalWords]);
  if (changes.length === 0) return null;
  const columns = ["word", "original", "old", "new", "source"];
  return (
    <div className="flex flex-col gap-2">
      <h3 className="text-sm font-semibold">{t("agent.proposals.roles.title")}</h3>
      <div className="overflow-x-auto">
        <table className="w-full text-left text-sm">
          <caption className="sr-only">{t("agent.proposals.roles.title")}</caption>
          <thead className="text-fg-muted">
            <tr className="border-b border-border">
              {columns.map((column) => (
                <th key={column} scope="col" className="px-2 py-1 font-medium">
                  {t(`agent.proposals.roles.${column}`)}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {changes.map((change) => {
              const word =
                change.stepWordId === undefined ? undefined : byId.get(change.stepWordId);
              return (
                <tr
                  key={`${change.elbWordId}-${change.stepWordId ?? 0}-${change.partOrder ?? 0}`}
                  className="border-b border-border align-top"
                >
                  <th scope="row" className="whitespace-nowrap px-2 py-1 font-medium" lang="de">
                    {change.german}
                  </th>
                  <td className="whitespace-nowrap px-2 py-1">
                    {change.original ?? "–"}
                    {word?.gloss ? (
                      <span className="block text-xs text-fg-muted">{word.gloss}</span>
                    ) : null}
                  </td>
                  <td className="px-2 py-1">
                    <RoleSide change={change} side="before" word={word} />
                  </td>
                  <td className="px-2 py-1">
                    <RoleSide change={change} side="new" word={word} />
                  </td>
                  <td className="px-2 py-1 text-fg-muted">
                    {t(
                      `agent.proposals.roles.${change.source === "rules" ? "sourceRules" : "sourceModel"}`,
                    )}
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
  );
}

interface ReviewBodyProps {
  diff: AgentProposalDiff;
  reference: string;
  onDone: () => void;
  onEditingChange?: (editing: boolean) => void;
}

function ReviewBody({ diff, reference, onDone, onEditingChange }: ReviewBodyProps) {
  const { t, i18n } = useTranslation();
  const queryClient = useQueryClient();
  const refresh = useRefreshAfterReview();
  const merge = useMergeProposal();
  const editMerge = useEditMergeProposal();
  const discard = useDiscardProposal();
  const [editing, setEditing] = useState(false);
  const [confirmDiscard, setConfirmDiscard] = useState(false);
  const [problems, setProblems] = useState<string[]>([]);
  const [lemmaEdits, setLemmaEdits] = useState<Record<number, LemmaEdit>>(() =>
    Object.fromEntries(
      diff.lemmaChanges.map((c) => [
        c.elbWordId,
        { lemma: c.lemma, stts: c.stts ?? "", skip: false },
      ]),
    ),
  );
  const confirmRef = useRef<HTMLButtonElement>(null);

  const open = diff.status === "open";
  const busy = merge.isPending || editMerge.isPending || discard.isPending;
  const liveChangedLemma = diff.lemmaChanges.some((c) => c.changed);
  const canMerge = open && !diff.stale && !liveChangedLemma && !busy;
  const canEdit = open && !diff.stale && !busy;

  const live = useMemo<VerseMappingLink[]>(() => liveLinks(diff), [diff]);
  const roleChanges = diff.roleChanges ?? [];
  const data = useMemo<GetVerseMappingResponse>(
    () => ({
      bibleBookId: diff.book,
      chapter: diff.chapter,
      verse: diff.verse,
      language: diff.language,
      editingEnabled: true,
      germanWords: diff.germanWords,
      originalWords: diff.originalWords,
      links: live,
    }),
    [diff, live],
  );

  useEffect(() => {
    onEditingChange?.(editing);
  }, [editing, onEditingChange]);

  async function succeeded() {
    await refresh({ runId: diff.runId, book: diff.book, chapter: diff.chapter, verse: diff.verse });
    onDone();
  }

  function failed(error: unknown) {
    setProblems(reviewProblems(error, t));
    if (error instanceof ApiError && error.status === 409) {
      // not open any more, or the verse changed: show what is live now
      setEditing(false);
      void queryClient.invalidateQueries({ queryKey: agentKeys.proposal(diff.id) });
      void queryClient.invalidateQueries({ queryKey: agentKeys.proposals(diff.runId) });
    }
  }

  function doMerge() {
    if (!canMerge) return;
    setProblems([]);
    merge.mutate(diff.id, { onSuccess: succeeded, onError: failed });
  }

  function startEdit() {
    if (!canEdit) return;
    setProblems([]);
    setEditing(true);
  }

  function doDiscard() {
    setProblems([]);
    discard.mutate(diff.id, {
      onSuccess: () => {
        setConfirmDiscard(false);
        return succeeded();
      },
      onError: (error) => {
        setConfirmDiscard(false);
        failed(error);
      },
    });
  }

  function saveEdited(links: VerseMappingLink[], options: { clearParts: boolean }) {
    const rows = diff.lemmaChanges.filter((c) => !lemmaEdits[c.elbWordId]?.skip);
    if (rows.some((c) => !lemmaEdits[c.elbWordId]?.lemma.trim())) {
      setProblems([t("agent.proposals.error.emptyLemma")]);
      return;
    }
    setProblems([]);
    editMerge.mutate(
      {
        id: diff.id,
        links,
        clearParts: options.clearParts,
        lemmas: rows.map((c) => {
          const edit = lemmaEdits[c.elbWordId];
          return {
            elbWordId: c.elbWordId,
            lemma: (edit?.lemma ?? c.lemma).trim(),
            ...(edit?.stts ? { stts: edit.stts } : {}),
            ...(c.partOfSpeech ? { partOfSpeech: c.partOfSpeech } : {}),
          };
        }),
      },
      { onSuccess: succeeded, onError: failed },
    );
  }

  // Review keys; the shared guard keeps them quiet while the maintainer types or works inside an editor or a dialog.
  const keyActions = useRef({ doMerge, startEdit });
  keyActions.current = { doMerge, startEdit };
  const canDiscard = open && !busy;
  useEffect(() => {
    function onKeyDown(event: KeyboardEvent) {
      if (editing || ignoresReviewKey(event)) return;
      if (event.key === "m") keyActions.current.doMerge();
      else if (event.key === "e") keyActions.current.startEdit();
      else if (event.key === "d" && canDiscard) setConfirmDiscard(true);
      else return;
      event.preventDefault();
    }
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, [editing, canDiscard]);

  const actions = (
    <div className="flex flex-col gap-2">
      <div className="flex flex-wrap items-center gap-2">
        <Button variant="primary" aria-keyshortcuts="m" disabled={!canMerge} onClick={doMerge}>
          {merge.isPending ? t("agent.proposals.merging") : t("agent.proposals.merge")}
          <Kbd>m</Kbd>
        </Button>
        <Button aria-keyshortcuts="e" disabled={!canEdit} onClick={startEdit}>
          {t("agent.proposals.edit")}
          <Kbd>e</Kbd>
        </Button>
        <Button
          variant="ghost"
          aria-keyshortcuts="d"
          disabled={!canDiscard}
          onClick={() => setConfirmDiscard(true)}
        >
          {t("agent.proposals.discard")}
          <Kbd>d</Kbd>
        </Button>
      </div>
      {open && liveChangedLemma ? (
        <p className="text-sm text-fg-muted">{t("agent.proposals.needsEdit")}</p>
      ) : null}
    </div>
  );

  return (
    <article
      aria-label={t("agent.proposals.reviewLabel", { reference })}
      className="flex flex-col gap-3"
    >
      <header className="flex flex-wrap items-center gap-x-3 gap-y-1">
        <h2 className="text-lg font-semibold">{reference}</h2>
        <StatusBadge tone={proposalTone(diff.status)}>
          {t(`agent.proposals.status.${diff.status}`)}
        </StatusBadge>
        <span className="text-sm text-fg-muted">{diff.model}</span>
        {diff.reviewedAt ? (
          <span className="text-sm text-fg-muted">
            {t("agent.proposals.reviewedAt", { at: formatStamp(diff.reviewedAt, i18n.language) })}
          </span>
        ) : null}
      </header>

      {diff.stale ? (
        <div role="alert" className="rounded-md border border-sev-warning bg-surface p-3 text-sm">
          <strong>{t("agent.proposals.stale")}</strong>{" "}
          {diff.staleReason ? <span>{diff.staleReason}</span> : null}
          <p className="text-fg-muted">{t("agent.proposals.staleHint")}</p>
        </div>
      ) : null}

      {!open && !diff.stale ? (
        <p role="status" className="text-sm text-fg-muted">
          {t("agent.proposals.notOpen", { status: t(`agent.proposals.status.${diff.status}`) })}
        </p>
      ) : null}

      {diff.note ? (
        <blockquote className="border-s-2 border-border ps-3 text-sm">
          <span className="block text-xs text-fg-muted">{t("agent.proposals.note")}</span>
          {diff.note}
        </blockquote>
      ) : null}

      {diff.flags.length > 0 ? (
        <section aria-label={t("agent.proposals.flags")} className="flex flex-col gap-1">
          <h3 className="text-sm font-semibold">
            {t("agent.proposals.flagsCount", { count: diff.flags.length })}
          </h3>
          <ul className="flex flex-col gap-1 text-sm">
            {diff.flags.map((flag) => {
              const label = t(`agent.flag.${flag.code}`, { defaultValue: flag.message });
              return (
                <li
                  key={`${flag.code}-${flag.elbWordId ?? 0}-${flag.stepWordId ?? 0}-${flag.message}`}
                >
                  <StatusBadge tone="warning">{label}</StatusBadge>
                  {label !== flag.message ? (
                    <span className="ms-2 text-fg-muted">{flag.message}</span>
                  ) : null}
                </li>
              );
            })}
          </ul>
        </section>
      ) : null}

      {editing ? (
        <MappingEditor
          data={data}
          reference={reference}
          partsEditable
          initialLinks={diff.proposedLinks}
          saveLabel={t("agent.proposals.saveEdited")}
          saving={editMerge.isPending}
          onSave={saveEdited}
          onCancel={() => setEditing(false)}
          footer={
            <>
              <RoleTable changes={roleChanges} originalWords={diff.originalWords} />
              <LemmaTable
                changes={diff.lemmaChanges}
                edits={lemmaEdits}
                onEdit={(id, edit) => setLemmaEdits((current) => ({ ...current, [id]: edit }))}
              />
            </>
          }
        />
      ) : (
        <>
          <MappingEditor
            mode="compare"
            data={data}
            reference={reference}
            compareLinks={diff.proposedLinks}
            onSave={() => {}}
            onCancel={() => {}}
            footer={actions}
          />
          <RoleTable changes={roleChanges} originalWords={diff.originalWords} />
          <LemmaTable changes={diff.lemmaChanges} />
          <VerseLemmaTable words={diff.verseLemmas ?? []} changes={diff.lemmaChanges} />
        </>
      )}

      {problems.map((message) => (
        <FormError key={message} message={message} />
      ))}

      <Dialog
        open={confirmDiscard}
        onClose={() => setConfirmDiscard(false)}
        title={t("agent.proposals.discardTitle")}
        description={t("agent.proposals.discardBody", { reference })}
        initialFocusRef={confirmRef}
        footer={
          <div className="flex flex-wrap justify-end gap-2">
            <Button variant="secondary" onClick={() => setConfirmDiscard(false)}>
              {t("agent.proposals.discardKeep")}
            </Button>
            <Button variant="primary" ref={confirmRef} disabled={busy} onClick={doDiscard}>
              {t("agent.proposals.discardConfirm")}
            </Button>
          </div>
        }
      >
        <p className="text-sm text-fg-muted">{t("agent.proposals.discardUndo")}</p>
      </Dialog>
    </article>
  );
}

export interface ProposalReviewProps {
  proposalId: number;
  /** After a merge or discard went through: the page moves on to the next open proposal. */
  onDone: () => void;
  /** True while the proposal is open in the editor (the page pauses its navigation keys). */
  onEditingChange?: (editing: boolean) => void;
}

/** Review panel of one proposal: stale banner, note, flags, the compare mapping, the lemma table and the three actions. */
export function ProposalReview({ proposalId, onDone, onEditingChange }: ProposalReviewProps) {
  const { t, i18n } = useTranslation();
  const diff = useAgentProposal(proposalId);
  const structure = useBibleStructure();
  if (diff.isError) {
    return (
      <p role="alert" className="text-sm text-fg-muted">
        {t("agent.proposals.loadFailed")}
      </p>
    );
  }
  if (!diff.data) {
    return <p className="text-sm text-fg-muted">{t("agent.proposals.loading")}</p>;
  }
  const book = structure.data?.find((b) => b.id === diff.data.book);
  const reference = `${book ? bookName(book, i18n.language) : `#${diff.data.book}`} ${diff.data.chapter},${diff.data.verse}`;
  return (
    <ReviewBody
      key={diff.data.id}
      diff={diff.data}
      reference={reference}
      onDone={onDone}
      onEditingChange={onEditingChange}
    />
  );
}
