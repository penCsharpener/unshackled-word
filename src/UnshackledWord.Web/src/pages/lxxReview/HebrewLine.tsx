import { useTranslation } from "react-i18next";
import { Button } from "@/components/Button";
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
}

/** The unit's Hebrew verse above its table, RTL; clicking a word toggles its link on the active Greek word (06a). */
export function HebrewLine({ words, activeLinks, unitLinks, enabled, onToggle }: HebrewLineProps) {
  const { t } = useTranslation();
  if (words.length === 0) {
    return <p className="text-sm text-fg-muted">{t("lxxReview.hebrew.addition")}</p>;
  }
  return (
    <div dir="rtl" lang="he" className="flex flex-wrap gap-x-2 gap-y-1 font-original text-2xl">
      {words.map((word) => (
        <Button
          key={word.id}
          variant="inline"
          disabled={!enabled}
          aria-pressed={activeLinks.has(word.id)}
          title={[word.strongs, word.gloss, word.morphology].filter(Boolean).join(" · ")}
          className={`${activeLinks.has(word.id) ? "outline-2 outline-accent" : ""} ${unitLinks.has(word.id) ? "bg-mark-same" : ""}`}
          onClick={() => onToggle(word.id)}
        >
          {word.text}
          <sub dir="ltr" className="font-sans text-xs text-fg-muted">
            {word.number}
          </sub>
          {unitLinks.has(word.id) ? (
            <span className="sr-only">{t("lxxReview.hebrew.linked")}</span>
          ) : null}
        </Button>
      ))}
    </div>
  );
}
