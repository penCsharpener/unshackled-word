import { useTranslation } from "react-i18next";
import type { GetVerseMappingResponse } from "@/lib/bible/types";
import { useSetHeaderInfo } from "@/lib/headerInfo";
import { Button } from "./Button";

export interface OriginalVerseProps {
  data: GetVerseMappingResponse;
  /** e.g. "Johannes 3,1" */
  reference: string;
  showLemma: boolean;
  showStrongs: boolean;
  showMorphology: boolean;
  onToggleLemma: () => void;
  onToggleStrongs: () => void;
  onToggleMorphology: () => void;
}

/** Plain original-language text of one verse for casual readers; Strong's and morphology can be added per word. */
export function OriginalVerse({
  data,
  reference,
  showLemma,
  showStrongs,
  showMorphology,
  onToggleLemma,
  onToggleStrongs,
  onToggleMorphology,
}: OriginalVerseProps) {
  const { t } = useTranslation();
  const setInfo = useSetHeaderInfo();
  const isRtl = data.language === "hebrew";
  const lang = isRtl ? "he" : "grc";
  const words = [...data.originalWords].sort((a, b) => a.position - b.position);

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
          {words.map((word) => (
            // biome-ignore lint/a11y/noStaticElementInteractions: mouse-only hover hint; the same data is in the word popover
            <span
              key={word.id}
              className="inline-flex flex-col items-center"
              onMouseEnter={() =>
                setInfo({
                  original: word.original,
                  strongs: word.strongs,
                  morphologyCode: word.morphologyCode,
                  language: data.language,
                })
              }
              onMouseLeave={() => setInfo(null)}
            >
              <span>{word.original}</span>
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
          ))}
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
