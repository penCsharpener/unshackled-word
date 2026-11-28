import { Fragment, useState } from "react";
import { useTranslation } from "react-i18next";
import { Button } from "@/components/Button";
import { FormError } from "@/components/FormError";
import { StatusBadge } from "@/components/StatusBadge";
import { formatStamp } from "@/lib/format";
import type {
  LxxReviewGermanWord,
  LxxReviewUnit,
  UnitDraft,
  WordDraft,
  WordDraftError,
} from "@/lib/maintainer/lxxReview";
import { HebrewLine } from "./HebrewLine";
import { WordTable } from "./WordTable";

export interface UnitCardProps {
  unit: LxxReviewUnit;
  draft: UnitDraft | undefined;
  /** Validation errors per word id (from `validateWordDraft`). */
  errors: ReadonlyMap<number, WordDraftError[]>;
  selected: boolean;
  activeWordId: number | undefined;
  editing: boolean;
  onSelect: (selected: boolean, shiftKey: boolean) => void;
  onActivate: (lxxWordId: number) => void;
  onChangeWord: (lxxWordId: number, patch: Partial<WordDraft>) => void;
  onToggleHebrew: (hebrewWordId: number) => void;
  onMorphologyValidity: (lxxWordId: number, code: string, valid: boolean | undefined) => void;
  onResetWord: (lxxWordId: number) => void;
  onConfirm: () => void;
  /** Opens the discard Dialog; only with a proposal. */
  onDiscard: () => void;
  /** 409: refetch the chapter and reset this unit's draft. */
  onReload: () => void;
  /** An edited parse code is still being checked: confirm waits. */
  checking?: boolean;
  queryRef?: React.Ref<HTMLInputElement>;
  codeRef?: React.Ref<HTMLInputElement>;
}

/**
 * One LXX unit (06a): header with selection checkbox, ref, badges and actions; Hebrew line; flags (a flag with a
 * position jumps to the word row); word table; proposal note; save state.
 */
export function UnitCard({
  unit,
  draft,
  errors,
  selected,
  activeWordId,
  editing,
  onSelect,
  onActivate,
  onChangeWord,
  onToggleHebrew,
  onMorphologyValidity,
  onResetWord,
  onConfirm,
  onDiscard,
  onReload,
  checking = false,
  queryRef,
  codeRef,
}: UnitCardProps) {
  const { t, i18n } = useTranslation();
  const saveState = draft?.saveState ?? "idle";
  const saving = saveState === "saving";
  const invalidCount = [...errors.values()].filter((list) => list.length > 0).length;
  const invalid = invalidCount > 0;
  const jump = (position: number) => {
    const word = unit.words.find((w) => w.position === position);
    if (!word) return;
    onActivate(word.id);
    const row = document.getElementById(`lxx-word-${word.id}`);
    row?.scrollIntoView?.({ block: "center" });
    row?.focus();
  };
  const activeWord = unit.words.find((w) => w.id === activeWordId);
  const activeLinks = new Set(
    activeWord ? (draft?.words.get(activeWord.id)?.hebrewWordIds ?? []) : [],
  );
  const unitLinks = new Set([...(draft?.words.values() ?? [])].flatMap((w) => w.hebrewWordIds));
  const [tipWordIds, setTipWordIds] = useState<ReadonlySet<number>>(new Set());

  return (
    <article
      id={`lxx-card-${unit.lxxVerseId}`}
      aria-labelledby={`lxx-unit-${unit.lxxVerseId}`}
      className="flex flex-col gap-3 rounded-lg border border-border p-3"
    >
      <header className="flex flex-wrap items-center gap-2">
        <label className="flex items-center gap-2">
          <input
            type="checkbox"
            checked={selected}
            onChange={(event) =>
              onSelect(
                event.target.checked,
                (event.nativeEvent as MouseEvent | KeyboardEvent).shiftKey ?? false,
              )
            }
          />
          <h2 id={`lxx-unit-${unit.lxxVerseId}`} className="font-medium">
            {unit.ref}
          </h2>
        </label>
        {unit.open ? (
          <StatusBadge tone="info">{t("lxxReview.badge.open")}</StatusBadge>
        ) : (
          <StatusBadge tone="ok">{t("lxxReview.badge.confirmed")}</StatusBadge>
        )}
        {unit.textForm ? (
          <StatusBadge tone="neutral">{t(`lxxReview.form.${unit.textForm}`)}</StatusBadge>
        ) : null}
        {unit.isAddition ? (
          <StatusBadge tone="neutral">{t("lxxReview.badge.addition")}</StatusBadge>
        ) : null}
        {unit.hasConfirmed ? (
          <StatusBadge tone="ok">{t("lxxReview.badge.hasConfirmed")}</StatusBadge>
        ) : null}
        {saveState === "saved" ? (
          <StatusBadge tone="ok">{t("lxxReview.badge.saved")}</StatusBadge>
        ) : null}
        {unit.proposal ? (
          <StatusBadge tone="neutral">
            {t("lxxReview.badge.run", { runId: unit.proposal.runId })} ·{" "}
            {formatStamp(unit.proposal.createdAt, i18n.language)}
          </StatusBadge>
        ) : null}
        <div className="ml-auto flex gap-2">
          {unit.proposal ? (
            <Button variant="secondary" size="sm" disabled={saving} onClick={onDiscard}>
              {t("lxxReview.action.discard")}
            </Button>
          ) : null}
          <Button
            variant="primary"
            size="sm"
            disabled={saving || invalid || checking || saveState === "conflict"}
            title={invalid ? t("lxxReview.error.invalidCount", { count: invalidCount }) : undefined}
            onClick={onConfirm}
          >
            {saving ? t("lxxReview.action.saving") : t("lxxReview.action.confirm")}
          </Button>
        </div>
      </header>

      <HebrewLine
        words={unit.hebrew}
        activeLinks={activeLinks}
        unitLinks={unitLinks}
        enabled={activeWord !== undefined}
        onToggle={onToggleHebrew}
        onTip={(word) => setTipWordIds(new Set(word?.germanWordIds ?? []))}
      />
      <GermanLine words={unit.german} marked={tipWordIds} />

      {unit.proposal && unit.proposal.flags.length > 0 ? (
        <ul className="flex flex-col gap-1 text-sm text-fg-muted">
          {unit.proposal.flags.map((flag) => (
            <li key={`${flag.code}:${flag.position ?? ""}:${flag.message}`}>
              {flag.position !== undefined ? (
                <Button variant="inline" onClick={() => jump(flag.position as number)}>
                  {flag.message}
                </Button>
              ) : (
                flag.message
              )}
            </li>
          ))}
        </ul>
      ) : null}

      <WordTable
        words={unit.words}
        hebrew={unit.hebrew}
        drafts={draft?.words ?? new Map()}
        errors={errors}
        activeWordId={activeWordId}
        editing={editing}
        onActivate={onActivate}
        onChange={onChangeWord}
        onMorphologyValidity={onMorphologyValidity}
        onResetWord={onResetWord}
        queryRef={queryRef}
        codeRef={codeRef}
      />

      {unit.proposal?.note ? <p className="text-sm text-fg-muted">{unit.proposal.note}</p> : null}

      {saveState === "conflict" ? (
        <div className="flex flex-wrap items-center gap-2">
          <FormError message={t("lxxReview.error.conflict")} />
          <Button variant="secondary" size="sm" onClick={onReload}>
            {t("lxxReview.action.reload")}
          </Button>
        </div>
      ) : null}
      <FormError
        message={saveState === "error" ? (draft?.error ?? t("lxxReview.error.save")) : ""}
      />
    </article>
  );
}

/** The Elb1871 verse of the unit's Hebrew verse, for reference; the words mapped to the Hebrew word under the tip are marked. */
function GermanLine({
  words,
  marked,
}: {
  words: LxxReviewGermanWord[];
  marked: ReadonlySet<number>;
}) {
  const { t } = useTranslation();
  if (words.length === 0) return null;
  return (
    <p
      dir="ltr"
      lang="de"
      data-testid="lxx-german-verse"
      className="font-serif text-sm text-fg-muted"
    >
      <span className="font-sans font-medium">{t("lxxReview.hebrew.germanVerse")}:</span>{" "}
      {words.map((word, index) => (
        <Fragment key={word.id}>
          {index > 0 ? " " : null}
          {marked.has(word.id) ? (
            <mark className="rounded-sm bg-mark-linked px-0.5 text-fg">{word.text}</mark>
          ) : (
            word.text
          )}
        </Fragment>
      ))}
    </p>
  );
}
