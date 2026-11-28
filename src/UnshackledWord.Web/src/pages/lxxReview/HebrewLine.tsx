import type { TFunction } from "i18next";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { Button } from "@/components/Button";
import { WordTipCard, type WordTipPosition, wordTipPosition } from "@/components/WordTipCard";
import { formatMorphology } from "@/lib/bible/morphology";
import type { LxxReviewHebrewWord } from "@/lib/maintainer/lxxReview";

export interface HebrewLineProps {
  words: LxxReviewHebrewWord[];
  /** Hebrew ids linked to the active Greek word (`aria-pressed`, accent outline). */
  activeLinks: ReadonlySet<number>;
  /** Hebrew ids linked to any word of the unit (`bg-mark-same`). */
  unitLinks: ReadonlySet<number>;
  /** False when the active word is in another unit: the buttons are disabled. */
  enabled: boolean;
  onToggle: (hebrewWordId: number) => void;
  /** The word whose tip is open (hover or focus), `undefined` when it closes; the card highlights its German words. */
  onTip?: (word: LxxReviewHebrewWord | undefined) => void;
}

/** Header line of the tip: Strong's · gloss · decoded morphology (falls back to the code). */
export function hebrewTipHead(word: LxxReviewHebrewWord, t: TFunction): string {
  const morphology = word.morphologyDetail
    ? formatMorphology(word.morphologyDetail, t)
    : word.morphology;
  return [word.strongs, word.gloss, morphology].filter(Boolean).join(" · ");
}

/** The unit's Hebrew verse above its table, RTL; clicking a word toggles its link on the active Greek word (06a). */
export function HebrewLine({
  words,
  activeLinks,
  unitLinks,
  enabled,
  onToggle,
  onTip,
}: HebrewLineProps) {
  const { t } = useTranslation();
  const [tip, setTip] = useState<{ word: LxxReviewHebrewWord; position: WordTipPosition }>();
  if (words.length === 0) {
    return <p className="text-sm text-fg-muted">{t("lxxReview.hebrew.addition")}</p>;
  }
  const show = (word: LxxReviewHebrewWord, element: HTMLElement) => {
    const position = wordTipPosition(element.getBoundingClientRect(), {
      width: window.innerWidth,
      height: window.innerHeight,
    });
    setTip({ word, position });
    onTip?.(word);
  };
  const hide = () => {
    setTip(undefined);
    onTip?.(undefined);
  };
  const tipId = (word: LxxReviewHebrewWord) => `lxx-hebrew-tip-${word.id}`;
  return (
    <>
      <div dir="rtl" lang="he" className="flex flex-wrap gap-x-2 gap-y-1 font-original text-2xl">
        {words.map((word) => (
          // biome-ignore lint/a11y/noStaticElementInteractions: mouse hover of a possibly disabled button; keyboard users get the tip on focus
          <span
            key={word.id}
            className="inline-flex"
            onMouseEnter={(event) => show(word, event.currentTarget)}
            onMouseLeave={hide}
          >
            <Button
              variant="inline"
              disabled={!enabled}
              aria-pressed={activeLinks.has(word.id)}
              aria-describedby={tip?.word.id === word.id ? tipId(word) : undefined}
              className={`${activeLinks.has(word.id) ? "outline-2 outline-accent" : ""} ${unitLinks.has(word.id) ? "bg-mark-same" : ""}`}
              onClick={() => onToggle(word.id)}
              onFocus={(event) => show(word, event.currentTarget)}
              onBlur={hide}
              onKeyDown={(event) => {
                if (event.key === "Escape" && tip) {
                  event.stopPropagation();
                  hide();
                }
              }}
            >
              {word.text}
              <sub dir="ltr" className="font-sans text-xs text-fg-muted">
                {word.number}
              </sub>
              {unitLinks.has(word.id) ? (
                <span className="sr-only">{t("lxxReview.hebrew.linked")}</span>
              ) : null}
            </Button>
          </span>
        ))}
      </div>
      {tip ? (
        <WordTipCard
          id={tipId(tip.word)}
          position={tip.position}
          original={tip.word.text}
          lang="he"
          head={hebrewTipHead(tip.word, t)}
          german={tip.word.german}
          germanLabel={t("lxxReview.hebrew.german")}
          note={tip.word.note}
        />
      ) : null}
    </>
  );
}
