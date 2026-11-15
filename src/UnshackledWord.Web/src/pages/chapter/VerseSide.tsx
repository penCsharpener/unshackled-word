import { memo } from "react";
import { useTranslation } from "react-i18next";
import { CrossReferenceList } from "@/components/CrossReferenceList";
import { OriginalVerse } from "@/components/OriginalVerse";
import { VerseSidePanel } from "@/components/VerseSidePanel";
import type { BibleStructureBook, ChapterOriginalVerse, CrossReference } from "@/lib/bible/types";

export interface VerseSideProps {
  mode: "tsk" | "original";
  verse: number;
  /** e.g. "Johannes 3,1" */
  reference: string;
  books?: BibleStructureBook[];
  /** `tsk`: references of the verse (absent = none) */
  references?: CrossReference[];
  expanded: boolean;
  onExpandedChange: (verse: number, expanded: boolean) => void;
  /** `original`: words and language of the verse (absent = none) */
  original?: ChapterOriginalVerse;
  language: "greek" | "hebrew";
  /** original word ids to highlight (linked to the hovered German word or original word) */
  linkedIds: ReadonlySet<number>;
  onHoverOriginal: (verse: number, id: number | undefined) => void;
}

/** The side panel content of one verse: its TSK references or its source text. Memoised, the page re-renders on every hover. */
export const VerseSide = memo(function VerseSide({
  mode,
  verse,
  reference,
  books,
  references,
  expanded,
  onExpandedChange,
  original,
  language,
  linkedIds,
  onHoverOriginal,
}: VerseSideProps) {
  const { t } = useTranslation();

  if (mode === "tsk") {
    if (!references || references.length === 0) return null;
    return (
      <VerseSidePanel
        mode="tsk"
        label={t("bible.sidePanel.verseTsk", { verse })}
        expanded={expanded}
        onExpandedChange={(next) => onExpandedChange(verse, next)}
      >
        <CrossReferenceList variant="inline" references={references} books={books} />
      </VerseSidePanel>
    );
  }

  return (
    <VerseSidePanel mode="original" label={t("bible.sidePanel.verseOriginal", { verse })}>
      {original && original.words.length > 0 ? (
        <OriginalVerse
          variant="margin"
          data={{ language, originalWords: original.words }}
          reference={reference}
          linkedIds={linkedIds}
          onHoverWord={(id) => onHoverOriginal(verse, id)}
        />
      ) : (
        <p className="text-fg-muted">{t("bible.sidePanel.noOriginal")}</p>
      )}
    </VerseSidePanel>
  );
});
