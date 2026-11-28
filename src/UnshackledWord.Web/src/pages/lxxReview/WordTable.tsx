import { Fragment } from "react";
import { useTranslation } from "react-i18next";
import { Button } from "@/components/Button";
import { StatusBadge } from "@/components/StatusBadge";
import type {
  LxxReviewHebrewWord,
  LxxReviewWord,
  WordDraft,
  WordDraftError,
} from "@/lib/maintainer/lxxReview";
import { useMediaQuery } from "@/lib/useMediaQuery";
import { WordEditor, WordEditorRow } from "./WordEditorRow";
import { type FieldView, viewWord } from "./wordView";

export interface WordTableProps {
  words: LxxReviewWord[];
  hebrew: LxxReviewHebrewWord[];
  drafts: ReadonlyMap<number, WordDraft>;
  errors: ReadonlyMap<number, WordDraftError[]>;
  activeWordId: number | undefined;
  /** Editor row open under the active word. */
  editing: boolean;
  onActivate: (lxxWordId: number) => void;
  /** The mouse is over a word row (`undefined` when it leaves): the unit underlines its linked Hebrew and German words. */
  onHoverWord?: (lxxWordId: number | undefined) => void;
  onChange: (lxxWordId: number, patch: Partial<WordDraft>) => void;
  onMorphologyValidity: (lxxWordId: number, code: string, valid: boolean | undefined) => void;
  onResetWord: (lxxWordId: number) => void;
  queryRef?: React.Ref<HTMLInputElement>;
  codeRef?: React.Ref<HTMLInputElement>;
}

const COLUMNS = [
  "position",
  "greek",
  "lemma",
  "strongs",
  "morphology",
  "hebrew",
  "confidence",
  "source",
] as const;

/** A tag field: the value with the "changed" mark and the stored value struck through when the AI differs. */
function FieldText({ field, original = false }: { field: FieldView; original?: boolean }) {
  const { t } = useTranslation();
  return (
    <span
      className={`inline-flex flex-col rounded-sm px-1 ${field.changed ? "bg-mark-same" : ""} ${field.invalid ? "border border-accent" : ""} ${original ? "original-text" : ""}`}
      aria-invalid={field.invalid || undefined}
    >
      <span>{field.value}</span>
      {field.stored ? <s className="text-xs text-fg-muted">{field.stored}</s> : null}
      {field.changed ? <span className="sr-only">{t("lxxReview.badge.changed")}</span> : null}
    </span>
  );
}

function HebrewCell({ view }: { view: ReturnType<typeof viewWord> }) {
  return (
    <span className={`rounded-sm px-1 ${view.hebrewChanged ? "bg-mark-same" : ""}`}>
      {view.hebrew.map((h) => (
        <span key={h.id} className="mr-1">
          {h.number}{" "}
          <span lang="he" className="original-text">
            {h.text}
          </span>
        </span>
      ))}
      {view.hebrewChanged ? <ChangedMark /> : null}
    </span>
  );
}

function ChangedMark() {
  const { t } = useTranslation();
  return <span className="sr-only">{t("lxxReview.badge.changed")}</span>;
}

function Confidence({ word }: { word: LxxReviewWord }) {
  const { t } = useTranslation();
  if (!word.ai) return null;
  return (
    <span className="inline-flex flex-wrap items-center gap-1">
      <StatusBadge tone={word.ai.confidence === "high" ? "ok" : "warning"}>
        {t(`lxxReview.badge.${word.ai.confidence}`)}
      </StatusBadge>
      {word.ai.prefilled ? (
        <span className="text-xs text-fg-muted">{t("lxxReview.badge.prefilled")}</span>
      ) : null}
    </span>
  );
}

function Source({ word }: { word: LxxReviewWord }) {
  const { t } = useTranslation();
  if (word.tag) {
    return (
      <StatusBadge tone="neutral">
        {t(`lxxReview.source.${word.tag.source}`)} · {t(`lxxReview.status.${word.tag.status}`)}
      </StatusBadge>
    );
  }
  if (word.ai) {
    return (
      <StatusBadge tone="neutral">
        {t("lxxReview.source.ai")} · {t("lxxReview.status.proposed")}
      </StatusBadge>
    );
  }
  return null;
}

/**
 * Word table of one unit (06a): Position | Griechisch | Lemma | Strong's | Bestimmung | Hebräisch | Sicherheit | Quelle.
 * Below md every word is a card (`ul`) with all fields, the editor opens inside the card. A row takes focus (Tab) and click to
 * become the active word; changed cells are marked, a stored value the AI replaced is struck through.
 */
export function WordTable(props: WordTableProps) {
  const { t } = useTranslation();
  const desktop = useMediaQuery("(min-width: 48rem)");
  const { words, hebrew, drafts, errors, activeWordId, editing, onActivate, onChange } = props;
  const hebrewById = new Map(hebrew.map((h) => [h.id, h]));
  const editorProps = (word: LxxReviewWord) => {
    const draft = drafts.get(word.id);
    if (!(word.id === activeWordId && editing && draft)) return undefined;
    return {
      draft,
      errors: errors.get(word.id) ?? [],
      onChange: (patch: Partial<WordDraft>) => onChange(word.id, patch),
      onMorphologyValidity: (code: string, valid: boolean | undefined) =>
        props.onMorphologyValidity(word.id, code, valid),
      onClearTag: () => onChange(word.id, { lemma: "", strongs: "", morphology: "" }),
      onReset: () => props.onResetWord(word.id),
      queryRef: props.queryRef,
      codeRef: props.codeRef,
    };
  };

  if (!desktop) {
    return (
      <ul className="flex flex-col gap-2">
        {words.map((word) => {
          const view = viewWord(word, drafts.get(word.id), errors.get(word.id) ?? [], hebrewById);
          const active = word.id === activeWordId;
          const form = editorProps(word);
          return (
            <li
              key={word.id}
              aria-current={active ? "true" : undefined}
              className={`flex flex-col gap-2 rounded-md border border-l-4 p-2 ${active ? "border-accent" : "border-border"}`}
            >
              <p className="flex items-baseline gap-2">
                <span className="text-sm text-fg-muted">{word.position}</span>
                <Button
                  id={`lxx-word-${word.id}`}
                  variant="inline"
                  className="original-text text-xl"
                  onFocus={() => onActivate(word.id)}
                  onClick={() => onActivate(word.id)}
                >
                  {word.greek}
                </Button>
                {view.invalid ? (
                  <StatusBadge tone="error">{t("lxxReview.badge.invalid")}</StatusBadge>
                ) : null}
              </p>
              <dl className="grid grid-cols-2 gap-x-3 gap-y-1 text-sm">
                <dt className="text-fg-muted">{t("lxxReview.col.lemma")}</dt>
                <dd>
                  <FieldText field={view.lemma} original />
                </dd>
                <dt className="text-fg-muted">{t("lxxReview.col.strongs")}</dt>
                <dd>
                  <FieldText field={view.strongs} />
                </dd>
                <dt className="text-fg-muted">{t("lxxReview.col.morphology")}</dt>
                <dd>
                  <FieldText field={view.morphology} />
                </dd>
                <dt className="text-fg-muted">{t("lxxReview.col.hebrew")}</dt>
                <dd>
                  <HebrewCell view={view} />
                </dd>
                <dt className="text-fg-muted">{t("lxxReview.col.confidence")}</dt>
                <dd>
                  <Confidence word={word} />
                </dd>
                <dt className="text-fg-muted">{t("lxxReview.col.source")}</dt>
                <dd>
                  <Source word={word} />
                </dd>
              </dl>
              {form ? <WordEditor {...form} /> : null}
            </li>
          );
        })}
      </ul>
    );
  }

  return (
    <table className="w-full text-sm">
      <thead>
        <tr className="text-left text-fg-muted">
          {COLUMNS.map((column) => (
            <th key={column} scope="col" className="px-2 py-1 font-medium">
              {t(`lxxReview.col.${column}`)}
            </th>
          ))}
        </tr>
      </thead>
      <tbody>
        {words.map((word) => {
          const view = viewWord(word, drafts.get(word.id), errors.get(word.id) ?? [], hebrewById);
          const active = word.id === activeWordId;
          const form = editorProps(word);
          return (
            <Fragment key={word.id}>
              <tr
                id={`lxx-word-${word.id}`}
                tabIndex={0}
                aria-current={active ? "true" : undefined}
                onFocus={() => onActivate(word.id)}
                onClick={() => onActivate(word.id)}
                onMouseEnter={() => props.onHoverWord?.(word.id)}
                onMouseLeave={() => props.onHoverWord?.(undefined)}
              >
                <td
                  className={`border-l-2 px-2 py-1 text-fg-muted ${active ? "border-accent" : "border-transparent"}`}
                >
                  {word.position}
                  {view.invalid ? (
                    <span className="sr-only">{t("lxxReview.badge.invalid")}</span>
                  ) : null}
                </td>
                <td className="original-text px-2 py-1">{word.greek}</td>
                <td className="px-2 py-1">
                  <FieldText field={view.lemma} original />
                </td>
                <td className="px-2 py-1">
                  <FieldText field={view.strongs} />
                </td>
                <td className="px-2 py-1">
                  <FieldText field={view.morphology} />
                </td>
                <td className="px-2 py-1">
                  <HebrewCell view={view} />
                </td>
                <td className="px-2 py-1">
                  <Confidence word={word} />
                </td>
                <td className="px-2 py-1">
                  <Source word={word} />
                </td>
              </tr>
              {form ? <WordEditorRow colSpan={COLUMNS.length} {...form} /> : null}
            </Fragment>
          );
        })}
      </tbody>
    </table>
  );
}
