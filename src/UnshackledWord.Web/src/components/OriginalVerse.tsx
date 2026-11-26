import { useEffect, useId, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import { editionVariant, filterWordsByEdition, mainEditionCodes } from "@/lib/bible/greekEditions";
import { useLxxWord } from "@/lib/bible/lxx";
import { useGreekEditions } from "@/lib/bible/queries";
import type {
  ChapterLexiconEntry,
  GetVerseMappingResponse,
  VerseMappingOriginalWord,
} from "@/lib/bible/types";
import { normalizeStrongs } from "@/lib/bible/wordDetail";
import { useSetHeaderInfo } from "@/lib/headerInfo";
import { Button } from "./Button";
import { WordInfoCard } from "./HeaderWordInfo";

export interface OriginalVerseProps {
  /**
   * `panel` (default): the eye panel under a verse with lemma / Strong's / morphology toggles.
   * `margin`: the same words in the side panel next to the verse, no frame and toggles, every word links to its Strong's page.
   */
  variant?: "panel" | "margin";
  /**
   * `language: "lxx"` (roadmap 0028.4f/0028.5f, `margin` only): Septuagint words, Greek font and `lang="grc"`. Tagged
   * words (`strongs`) are links with the hover card; a plain click pins the card with the details of `GET lxx/word/{id}`.
   * Untagged words are plain-looking buttons that pin a card saying "Noch nicht bestimmt". Proposed tags (maintainers
   * only, `lxx.status`) get a dotted underline and a "Vorschlag" badge.
   */
  data: {
    language: GetVerseMappingResponse["language"] | "lxx";
    originalWords: GetVerseMappingResponse["originalWords"];
  };
  /** `margin`: English gloss and lexicon text per Strong's number for the hover card (chapter originals `lexicon`). */
  lexicon?: Record<string, ChapterLexiconEntry>;
  /** e.g. "Johannes 3,1" */
  reference: string;
  /** Original words to highlight (linked to the hovered German word). */
  linkedIds?: ReadonlySet<number>;
  /** Called with the hovered original word id, or undefined when the pointer leaves. */
  onHoverWord?: (id: number | undefined) => void;
  /**
   * `margin`, Greek: main edition code of the reader's edition filter (roadmap 0025.6f); words whose `editionCodes` lack it
   * are hidden and a muted line counts them. Null/absent = all words.
   */
  edition?: string | null;
  showLemma?: boolean;
  showStrongs?: boolean;
  showMorphology?: boolean;
  onToggleLemma?: () => void;
  onToggleStrongs?: () => void;
  onToggleMorphology?: () => void;
}

const TIP_WIDTH = 256; // keep in sync with `w-64`
const TIP_GAP = 8;
/** Closes the card of the verse that shows one: only one card exists at a time, quick moves over many verses leave no trail. */
let closeShownTip: (() => void) | undefined;

const TIP_HIDE_DELAY = 500; // ms, long enough to move from the word onto the card

/** Greek word not in all main editions (roadmap 0025.5f): neutral dotted underline, never a warning colour. */
const VARIANT_MARK =
  "underline decoration-mark-variant decoration-dotted decoration-2 underline-offset-4";

/** Plain original-language text of one verse for casual readers; Strong's and morphology can be added per word. */
export function OriginalVerse({
  variant = "panel",
  data,
  lexicon,
  reference,
  linkedIds,
  onHoverWord,
  edition = null,
  showLemma = false,
  showStrongs = false,
  showMorphology = false,
  onToggleLemma,
  onToggleStrongs,
  onToggleMorphology,
}: OriginalVerseProps) {
  const { t } = useTranslation();
  const setInfo = useSetHeaderInfo();
  const isRtl = data.language === "hebrew";
  const lang = isRtl ? "he" : "grc";
  const sorted = [...data.originalWords].sort((a, b) => a.position - b.position);
  // the edition filter only applies to the side panel and to Greek (Hebrew words carry no editionCodes)
  const { visible: words, hidden } = filterWordsByEdition(
    sorted,
    variant === "margin" && data.language === "greek" ? edition : null,
  );
  // edition facts of Greek words (chapter originals); the catalog only loads when a word carries them
  const hasVariants = data.language === "greek" && words.some((w) => w.editionCodes);
  const { data: catalog } = useGreekEditions(hasVariants);
  const mainCodes = mainEditionCodes(catalog);
  const idBase = useId();
  /** "nur in TR, Byz" / "fehlt in WH, Treg"; undefined for words in all main editions and shift-only words */
  const editionNote = (word: VerseMappingOriginalWord): string | undefined => {
    if (!hasVariants) return undefined;
    const variant = editionVariant(word.editionCodes, mainCodes);
    if (!variant) return undefined;
    return t(
      variant.kind === "only"
        ? "bible.wordDetail.editions.markOnly"
        : "bible.wordDetail.editions.markMissing",
      { codes: variant.codes.join(", ") },
    );
  };
  // margin variant: lemma, Strong's and morphology of the hovered word as a card above it (data is already loaded with the chapter)
  // `top`/`bottom`: viewport edges of the hovered word; the card sits above it, or below when it does not fit above
  const [tip, setTip] = useState<{
    id: number;
    x: number;
    top: number;
    bottom: number;
    /** Septuagint detail: stays until closed (button, Escape, another word) */
    pinned?: boolean;
  } | null>(null);
  const tipWord = tip ? words.find((w) => w.id === tip.id) : undefined;
  // the card stays while the pointer (or focus) moves from the word onto it, so its Strong's links can be clicked
  const hideTimer = useRef<ReturnType<typeof setTimeout> | undefined>(undefined);
  // Ctrl held = pin: the card stays until Ctrl is released and the pointer is off word and card
  const ctrlHeld = useRef(false);
  const pinnedRef = useRef(false);
  const cancelHide = () => clearTimeout(hideTimer.current);
  const closeNow = useRef(() => {
    clearTimeout(hideTimer.current);
    pinnedRef.current = false;
    setTip(null);
  }).current;
  const scheduleHide = () => {
    cancelHide();
    hideTimer.current = setTimeout(() => {
      if (pinnedRef.current) return;
      if (ctrlHeld.current) {
        scheduleHide();
        return;
      }
      setTip(null);
    }, TIP_HIDE_DELAY);
  };
  // biome-ignore lint/correctness/useExhaustiveDependencies: listeners and cleanup only
  useEffect(() => {
    const track = (event: KeyboardEvent) => {
      ctrlHeld.current = event.ctrlKey;
      if (event.key === "Escape" && pinnedRef.current) closeNow();
    };
    window.addEventListener("keydown", track);
    window.addEventListener("keyup", track);
    return () => {
      window.removeEventListener("keydown", track);
      window.removeEventListener("keyup", track);
      cancelHide();
      if (closeShownTip === closeNow) closeShownTip = undefined;
    };
  }, []);

  const isLxx = data.language === "lxx";
  // the pinned Septuagint card adds the TBESG definition from the word detail
  const { data: lxxDetail } = useLxxWord(isLxx && tip?.pinned ? tip.id : undefined);

  if (variant === "margin") {
    return (
      <>
        <p
          dir={isRtl ? "rtl" : "ltr"}
          lang={lang}
          className={`font-original leading-snug ${isRtl ? "text-3xl" : "text-2xl"}`}
        >
          {words.map((word) => {
            const strongs = word.strongs ? normalizeStrongs(word.strongs) : undefined;
            const highlight = linkedIds?.has(word.id) ? "bg-mark-linked" : "";
            const note = editionNote(word);
            const mark = note ? VARIANT_MARK : "";
            const noteId = note ? `${idBase}-edition-${word.id}` : undefined;
            const enter = (element: HTMLElement, pinned = false) => {
              // a pinned card is not replaced by hovering other words
              if (pinnedRef.current && !pinned) return;
              cancelHide();
              pinnedRef.current = pinned;
              if (closeShownTip && closeShownTip !== closeNow) closeShownTip();
              closeShownTip = closeNow;
              onHoverWord?.(word.id);
              const rect = element.getBoundingClientRect();
              setTip({
                id: word.id,
                x: Math.max(TIP_GAP, Math.min(rect.left, window.innerWidth - TIP_WIDTH - TIP_GAP)),
                top: rect.top,
                bottom: rect.bottom,
                pinned: pinned || undefined,
              });
            };
            const leave = () => {
              onHoverWord?.(undefined);
              // delayed, not immediate: the pointer needs time to cross onto the card (or Ctrl pins it)
              scheduleHide();
            };
            const hover = {
              onMouseEnter: (event: { currentTarget: HTMLElement }) => enter(event.currentTarget),
              onMouseLeave: leave,
              onFocus: (event: { currentTarget: HTMLElement }) => enter(event.currentTarget),
              onBlur: leave,
            };
            // Septuagint: a plain click opens the detail card; modified clicks still follow the link
            const openDetail = (element: HTMLElement) => enter(element, true);
            const proposed = isLxx && word.lxx?.status === "proposed";
            const lxxMark = proposed ? VARIANT_MARK : "";
            if (isLxx && !strongs) {
              return (
                <span key={word.id}>
                  <Button
                    variant="inline"
                    aria-label={t("bible.sidePanel.lxx.untaggedLink", { original: word.original })}
                    aria-expanded={tip?.pinned && tip.id === word.id}
                    className={`hover:underline ${highlight}`}
                    onClick={(event) => openDetail(event.currentTarget)}
                    onMouseEnter={() => onHoverWord?.(word.id)}
                    onMouseLeave={() => onHoverWord?.(undefined)}
                    onFocus={() => onHoverWord?.(word.id)}
                    onBlur={() => onHoverWord?.(undefined)}
                  >
                    {word.original}
                  </Button>{" "}
                </span>
              );
            }
            return (
              <span key={word.id}>
                {strongs ? (
                  <Link
                    to={`/bible/strongs/${strongs}`}
                    aria-label={t("bible.sidePanel.strongsLink", {
                      original: word.original,
                      strongs,
                    })}
                    aria-describedby={noteId}
                    className={`rounded-sm hover:underline ${mark} ${lxxMark} ${highlight}`}
                    onClick={(event) => {
                      if (!isLxx || event.button !== 0) return;
                      if (event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
                      event.preventDefault();
                      openDetail(event.currentTarget);
                    }}
                    {...hover}
                  >
                    {word.original}
                  </Link>
                ) : (
                  <span
                    aria-describedby={noteId}
                    className={`rounded-sm ${mark} ${highlight}`}
                    {...hover}
                  >
                    {word.original}
                  </span>
                )}
                {note && (
                  <span id={noteId} hidden>
                    {note}
                  </span>
                )}{" "}
              </span>
            );
          })}
        </p>
        {hidden > 0 && edition && (
          <p
            title={t("bible.sidePanel.edition.info")}
            className="mt-1 font-sans text-xs text-fg-muted"
          >
            {t("bible.sidePanel.hiddenWords", { count: hidden, edition })}
          </p>
        )}
        {tip && tipWord && (
          // biome-ignore lint/a11y/noStaticElementInteractions: keeps the card open while the pointer is on it; the links inside are the interactive parts
          <div
            onMouseEnter={cancelHide}
            onMouseLeave={scheduleHide}
            onFocus={cancelHide}
            onBlur={scheduleHide}
            data-testid="original-word-info"
            ref={(element) => {
              if (!element) return;
              // measured pixel position: the only value that cannot be a design token
              element.style.setProperty("left", `${tip.x}px`);
              // above the word; below it when the card would run out of the top of the viewport
              const below = element.offsetHeight > tip.top;
              element.toggleAttribute("data-below", below);
              element.style.setProperty("top", `${below ? tip.bottom : tip.top}px`);
            }}
            className="fixed z-50 w-64 -translate-y-full pb-2 font-sans text-base data-below:translate-y-0 data-below:pt-2 data-below:pb-0"
          >
            <WordInfoCard
              onClose={tip.pinned ? closeNow : undefined}
              info={{
                original: tipWord.original,
                lemma: tipWord.lemma,
                strongs: tipWord.strongs,
                morphology: tipWord.morphology,
                morphologyCode: tipWord.morphologyCode,
                language: data.language === "lxx" ? "greek" : data.language,
                gloss:
                  tipWord.gloss ??
                  (tipWord.strongs ? lexicon?.[tipWord.strongs]?.gloss : undefined),
                definition: tipWord.strongs
                  ? (lexicon?.[tipWord.strongs]?.definition ?? lxxDetail?.tag?.lexicon?.definition)
                  : undefined,
                definitionSegments: tipWord.strongs
                  ? (lexicon?.[tipWord.strongs]?.definitionSegments ??
                    lxxDetail?.tag?.lexicon?.definitionSegments)
                  : undefined,
                editionNote: editionNote(tipWord),
                ...(tipWord.lxx && {
                  notes: [
                    ...(tip.pinned ? [tipWord.lxx.ref] : []),
                    ...(tipWord.strongs ? [] : [t("bible.sidePanel.lxx.untagged")]),
                    ...(tipWord.lxx.status === "proposed"
                      ? [t("bible.sidePanel.lxx.proposalHint")]
                      : []),
                  ],
                  badge:
                    tipWord.lxx.status === "proposed"
                      ? t("bible.sidePanel.lxx.proposal")
                      : undefined,
                }),
              }}
            />
          </div>
        )}
      </>
    );
  }

  return (
    <section
      aria-label={t("bible.mapping.originalPanelLabel", { reference })}
      className="map-panel"
    >
      <div className="map-sec flex flex-wrap items-start justify-between gap-3">
        <p
          dir={isRtl ? "rtl" : "ltr"}
          lang={lang}
          className="flex flex-wrap gap-x-3 gap-y-2 font-original text-2xl leading-tight"
        >
          {words.map((word) => {
            const note = editionNote(word);
            const noteId = note ? `${idBase}-edition-${word.id}` : undefined;
            return (
              // biome-ignore lint/a11y/noStaticElementInteractions: mouse-only hover hint; the same data is in the word popover
              <span
                key={word.id}
                className={`inline-flex flex-col items-center rounded-sm ${linkedIds?.has(word.id) ? "bg-mark-linked" : ""}`}
                onMouseEnter={() => {
                  onHoverWord?.(word.id);
                  setInfo({
                    original: word.original,
                    strongs: word.strongs,
                    morphology: word.morphology,
                    morphologyCode: word.morphologyCode,
                    language: data.language === "lxx" ? "greek" : data.language,
                    editionNote: note,
                  });
                }}
                onMouseLeave={() => {
                  onHoverWord?.(undefined);
                  setInfo(null);
                }}
              >
                <span aria-describedby={noteId} className={note ? VARIANT_MARK : undefined}>
                  {word.original}
                </span>
                {note && (
                  <span id={noteId} hidden>
                    {note}
                  </span>
                )}
                {showLemma && word.lemma && (
                  <span className="text-base text-fg-muted">{word.lemma}</span>
                )}
                {showStrongs && word.strongs && (
                  <code className="font-sans text-xs text-fg-muted" dir="ltr">
                    {word.strongs}
                  </code>
                )}
                {showMorphology && word.morphologyCode && (
                  <code className="font-sans text-xs text-fg-muted" dir="ltr">
                    {word.morphologyCode}
                  </code>
                )}
              </span>
            );
          })}
        </p>
        <div className="flex gap-2">
          <Button variant="secondary" size="sm" aria-pressed={showLemma} onClick={onToggleLemma}>
            {t("bible.mapping.showLemma")}
          </Button>
          <Button
            variant="secondary"
            size="sm"
            aria-pressed={showStrongs}
            onClick={onToggleStrongs}
          >
            {t("bible.mapping.showStrongs")}
          </Button>
          <Button
            variant="secondary"
            size="sm"
            aria-pressed={showMorphology}
            onClick={onToggleMorphology}
          >
            {t("bible.mapping.showMorphology")}
          </Button>
        </div>
      </div>
    </section>
  );
}
