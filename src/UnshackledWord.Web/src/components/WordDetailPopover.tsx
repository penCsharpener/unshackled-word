import { useEffect, useId, useLayoutEffect, useRef } from "react";
import { useTranslation } from "react-i18next";
import { formatMorphology } from "@/lib/bible/morphology";
import type {
  CoMappedWord,
  GetWordDetailResponse,
  MappingProvenance,
  MappingStatus,
  Morpheme,
  OriginalLanguage,
  OriginalReading,
  WordToken,
} from "@/lib/bible/types";
import { mappingStatus } from "@/lib/bible/wordDetail";
import { Button } from "./Button";

export type WordDetailCloseReason = "escape" | "button" | "outside";

export interface WordDetailPopoverProps {
  /** id for aria-controls on the trigger */
  id: string;
  /** the clicked Elberfelder word (header text while loading) */
  word: WordToken;
  status: "pending" | "error" | "success";
  detail?: GetWordDetailResponse;
  /** show the "sample data" badge (mock source) */
  isSample?: boolean;
  onRetry: () => void;
  onClose: (reason: WordDetailCloseReason) => void;
  /** opens the mapping editor; until the tagging UI exists the action is shown disabled */
  onEditMapping?: () => void;
}

const LANG_TAG: Record<OriginalLanguage, string> = { greek: "grc", hebrew: "he", aramaic: "arc" };
const dirOf = (language: OriginalLanguage) => (language === "greek" ? undefined : "rtl");

const STATUS_CLASSES: Record<MappingStatus, string> = {
  aiSuggested: "border-border text-fg-muted",
  confirmed: "border-accent text-accent",
  manual: "border-accent text-accent",
};

/** The German words that are translated together, in verse order; the open word is emphasised. */
function Phrase({ detail, coMapped }: { detail: GetWordDetailResponse; coMapped: CoMappedWord[] }) {
  const { t } = useTranslation();
  const words = [
    ...coMapped.map((w) => ({ id: w.wordId, text: w.wordInContext, position: w.positionInVerse })),
    { id: detail.wordId, text: detail.wordInContext, position: detail.positionInVerse },
  ].sort((a, b) => a.position - b.position);
  return (
    <p className="mt-2 text-sm text-fg-muted">
      {t("bible.wordDetail.phrase")}{" "}
      <span lang="de" className="font-scripture text-base text-fg">
        {words.map((w, i) => (
          <span key={w.id}>
            {i > 0 && " "}
            {w.id === detail.wordId ? <strong>{w.text}</strong> : w.text}
          </span>
        ))}
      </span>
    </p>
  );
}

function MappingFooter({
  mapping,
  onEditMapping,
}: {
  mapping?: MappingProvenance;
  onEditMapping?: () => void;
}) {
  const { t, i18n } = useTranslation();
  const date = (iso: string) =>
    new Intl.DateTimeFormat(i18n.language, { dateStyle: "medium", timeZone: "UTC" }).format(
      new Date(iso),
    );
  return (
    <div className="mt-4 border-t border-border pt-3 text-xs text-fg-muted">
      {mapping && (
        <p>
          {t(`bible.wordDetail.mapping.since.${mapping.source}`, { date: date(mapping.createdAt) })}
          {mapping.model && <> ({mapping.model})</>}
          {mapping.confirmedAt && (
            <> · {t("bible.wordDetail.mapping.confirmedAt", { date: date(mapping.confirmedAt) })}</>
          )}
        </p>
      )}
      <div className="mt-2 flex flex-wrap items-center gap-2">
        <Button variant="secondary" size="sm" disabled={!onEditMapping} onClick={onEditMapping}>
          {t("bible.wordDetail.mapping.edit")}
        </Button>
        {!onEditMapping && <span>{t("bible.wordDetail.mapping.editSoon")}</span>}
      </div>
    </div>
  );
}

function ReadingSection({
  reading,
  isFirst,
  sharedWith,
}: {
  reading: OriginalReading;
  isFirst: boolean;
  /** German words that share this original word, shown when there are several readings */
  sharedWith?: string[];
}) {
  const { t } = useTranslation();
  const headingId = useId();
  const lang = LANG_TAG[reading.language];
  const dir = dirOf(reading.language);
  const label = (key: string) => t(`bible.wordDetail.field.${key}`);

  return (
    <section
      aria-labelledby={headingId}
      className={isFirst ? "" : "mt-4 border-t border-border pt-4"}
    >
      <h3 id={headingId} className="sr-only">
        {t(`bible.wordDetail.language.${reading.language}`)} {reading.original}
      </h3>
      {reading.germanWordPart && (
        <p className="text-sm text-fg-muted">
          {t("bible.wordDetail.germanPart", { part: reading.germanWordPart })}
        </p>
      )}
      {sharedWith && sharedWith.length > 0 && (
        <p className="text-sm text-fg-muted">
          {t("bible.wordDetail.sharedWith", { words: sharedWith.join(", ") })}
        </p>
      )}
      <p lang={lang} dir={dir} className="font-original text-2xl">
        {reading.original}
      </p>
      {reading.transliteration && <p className="italic text-fg-muted">{reading.transliteration}</p>}
      <dl className="mt-2 grid grid-cols-2 gap-x-3 gap-y-1 text-sm">
        <dt className="text-fg-muted">{label("strongs")}</dt>
        <dd>{reading.strongs}</dd>
        {reading.lemma && (
          <>
            <dt className="text-fg-muted">{label("lemma")}</dt>
            <dd lang={lang} dir={dir}>
              {reading.lemma}
            </dd>
          </>
        )}
        {reading.morphology && (
          <>
            <dt className="text-fg-muted">{label("morphology")}</dt>
            <dd>
              {formatMorphology(reading.morphology, t)}{" "}
              <code className="text-xs text-fg-muted">{reading.morphology.code}</code>
            </dd>
          </>
        )}
        {reading.gloss && (
          <>
            <dt className="text-fg-muted">{label("gloss")}</dt>
            <dd lang="en">{reading.gloss}</dd>
          </>
        )}
        {reading.contextGloss && (
          <>
            <dt className="text-fg-muted">{label("contextGloss")}</dt>
            <dd lang="en">{reading.contextGloss}</dd>
          </>
        )}
      </dl>
      {reading.morphemes && reading.morphemes.length > 0 && (
        <div className="mt-2 text-sm">
          <p className="text-fg-muted">{label("morphemes")}</p>
          <ol className="mt-1 space-y-1">
            {reading.morphemes.map((m: Morpheme, i) => (
              // biome-ignore lint/suspicious/noArrayIndexKey: morphemes are static and ordered by word position
              <li key={`${m.role}-${i}`} className="flex flex-wrap items-baseline gap-x-2">
                <span className="text-fg-muted">{t(`bible.wordDetail.morpheme.${m.role}`)}</span>
                <span lang={lang} dir={dir} className="font-original text-lg">
                  {m.original}
                </span>
                {m.strongs && <span>{m.strongs}</span>}
                {m.gloss && <span lang="en">{m.gloss}</span>}
                {m.morphology && (
                  <span className="text-fg-muted">{formatMorphology(m.morphology, t)}</span>
                )}
              </li>
            ))}
          </ol>
        </div>
      )}
      {reading.lexicon?.definition && (
        <details className="mt-2 text-sm">
          <summary className="cursor-pointer text-fg-muted">{label("definition")}</summary>
          <p lang="en" className="mt-1">
            {reading.lexicon.definition}
            {reading.lexicon.definitionTruncated && (
              <span className="text-fg-muted"> {t("bible.wordDetail.truncated")}</span>
            )}
          </p>
        </details>
      )}
    </section>
  );
}

export function WordDetailPopover({
  id,
  word,
  status,
  detail,
  isSample,
  onRetry,
  onClose,
  onEditMapping,
}: WordDetailPopoverProps) {
  const { t } = useTranslation();
  const headingId = useId();
  const rootRef = useRef<HTMLDivElement>(null);
  const onCloseRef = useRef(onClose);
  onCloseRef.current = onClose;

  useLayoutEffect(() => {
    const el = rootRef.current;
    if (!el) return;
    if (typeof el.showPopover === "function") el.showPopover();
    el.focus();
  }, []);

  useEffect(() => {
    function onKeyDown(event: KeyboardEvent) {
      if (event.key !== "Escape") return;
      event.preventDefault();
      event.stopPropagation();
      onCloseRef.current("escape");
    }
    function onPointerDown(event: PointerEvent) {
      const target = event.target;
      if (!(target instanceof Node)) return;
      if (rootRef.current?.contains(target)) return;
      if (target instanceof Element && target.closest("[data-word-trigger]")) return;
      onCloseRef.current("outside");
    }
    document.addEventListener("keydown", onKeyDown, true);
    document.addEventListener("pointerdown", onPointerDown);
    return () => {
      document.removeEventListener("keydown", onKeyDown, true);
      document.removeEventListener("pointerdown", onPointerDown);
    };
  }, []);

  const readings = status === "success" ? (detail?.readings ?? []) : [];
  const success = status === "success" && detail !== undefined;
  const coMapped = success ? (detail.coMappedWords ?? []) : [];
  const parent = coMapped.find((w) => w.relation === "parent");
  const mapStatus = success && detail.mapping ? mappingStatus(detail.mapping) : undefined;
  const wordText = (id: number) => coMapped.find((w) => w.wordId === id)?.wordInContext;

  return (
    <div
      id={id}
      ref={rootRef}
      popover="manual"
      role="dialog"
      aria-labelledby={headingId}
      tabIndex={-1}
      className="word-popover border border-border bg-surface p-4 text-fg shadow-lg"
    >
      <div className="flex items-start justify-between gap-2">
        <h2 id={headingId} lang="de" className="font-scripture text-xl">
          <span className="sr-only">{t("bible.wordDetail.dialogLabel", { word: word.text })}</span>
          <span aria-hidden="true">{word.text}</span>
        </h2>
        <div className="flex items-center gap-2">
          {mapStatus && (
            <span className={`rounded-sm border px-1 text-xs ${STATUS_CLASSES[mapStatus]}`}>
              {t(`bible.wordDetail.mapping.status.${mapStatus}`)}
            </span>
          )}
          {isSample && (
            <span className="rounded-sm bg-surface-muted px-1 text-xs text-fg-muted">
              {t("bible.wordDetail.sample")}
            </span>
          )}
          <Button
            variant="ghost"
            size="sm"
            aria-label={t("bible.wordDetail.close")}
            onClick={() => onClose("button")}
          >
            ×
          </Button>
        </div>
      </div>

      {status === "pending" && (
        <p aria-busy="true" className="mt-2 text-fg-muted">
          {t("bible.wordDetail.loading")}
        </p>
      )}
      {status === "error" && (
        <div role="alert" className="mt-2 flex flex-wrap items-center gap-3">
          <p>{t("bible.wordDetail.error")}</p>
          <Button variant="secondary" size="sm" onClick={onRetry}>
            {t("bible.retry")}
          </Button>
        </div>
      )}
      {success && coMapped.length > 0 && <Phrase detail={detail} coMapped={coMapped} />}
      {status === "success" && readings.length === 0 && (
        <p className="mt-2 text-fg-muted">
          {detail?.isAddedWord && parent
            ? t("bible.wordDetail.addedWordFor", { parent: parent.wordInContext })
            : t(detail?.isAddedWord ? "bible.wordDetail.addedWord" : "bible.wordDetail.noMapping")}
        </p>
      )}
      {success &&
        detail.hints?.map((hint) => (
          <p key={hint} role="note" className="mt-2 text-sm text-fg-muted">
            {t(`bible.wordDetail.hint.${hint}`)}
          </p>
        ))}
      {readings.length > 0 && (
        <div className="mt-2">
          {readings.length > 1 && (
            <p className="mb-2 text-sm text-fg-muted">
              {t("bible.wordDetail.readings", { count: readings.length })}
            </p>
          )}
          {readings.map((reading, index) => (
            <ReadingSection
              key={`${reading.stepWordId}-${reading.strongs}`}
              reading={reading}
              isFirst={index === 0}
              sharedWith={
                readings.length > 1
                  ? reading.sharedWithWordIds?.map(wordText).filter((w) => w !== undefined)
                  : undefined
              }
            />
          ))}
          <p className="mt-4 text-xs text-fg-muted">{t("bible.wordDetail.attribution")}</p>
        </div>
      )}
      {success && <MappingFooter mapping={detail.mapping} onEditMapping={onEditMapping} />}
    </div>
  );
}
