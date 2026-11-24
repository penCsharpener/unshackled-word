import { memo, useMemo } from "react";
import { useTranslation } from "react-i18next";
import { type CrossReferenceLinkMode, CrossReferenceList } from "@/components/CrossReferenceList";
import { OriginalVerse } from "@/components/OriginalVerse";
import { VerseSidePanel } from "@/components/VerseSidePanel";
import type {
  BibleStructureBook,
  ChapterOriginalVerse,
  CrossReference,
  GetChapterOriginalsResponse,
} from "@/lib/bible/types";

const NO_KEYS: ReadonlySet<string> = new Set();

export interface VerseLinkMode {
  dragging: boolean;
  onSelectedChange: (verse: number, keys: string[]) => void;
  onDragStart: (verse: number, references: CrossReference[]) => void;
  onDragEnd: () => void;
}

export interface VerseSideProps {
  mode: "tsk" | "original";
  verse: number;
  /** chapter and reader level, for the hover text of the TSK links */
  bookId: number;
  chapter: number;
  level: number;
  /** e.g. "Johannes 3,1" */
  reference: string;
  books?: BibleStructureBook[];
  /** `tsk`: references of the verse (absent = none) */
  references?: CrossReference[];
  /** `tsk`: "Nur hervorgehobene" — list only highlighted links; a verse without any shows nothing. */
  onlyHighlighted?: boolean;
  /** `tsk`, curate mode (Admin/Maintainer): hidden links are listed greyed. */
  showHidden?: boolean;
  /**
   * `tsk`: pen per link opening the `CrossReferenceEditor` and "Querverweis hinzufügen" after the list
   * (every logged-in user); with `showHidden` also verses without links get a panel.
   */
  editable?: boolean;
  /** `tsk`, logged-in non-maintainers (roadmap 0024.6f): the editor sends suggestions. */
  suggest?: boolean;
  /**
   * `tsk`, link mode (roadmap 0024.5f, every logged-in user since 0024.6f): checkboxes and drag per link; one object for all verses,
   * the callbacks get the verse.
   */
  link?: VerseLinkMode;
  /** Link mode: selected keys of this verse (a shared empty set for the other verses, so the memo holds). */
  linkSelected?: ReadonlySet<string>;
  /** `tsk`: keys of links that flash because their word is hovered in the verse text. */
  flashKeys?: ReadonlySet<string>;
  /** `tsk`: a link of the verse is hovered (`undefined` = none); the reader marks its word. */
  onHoverReference?: (verse: number, reference: CrossReference | undefined) => void;
  /** `original`: words and language of the verse (absent = none) */
  original?: ChapterOriginalVerse;
  language: "greek" | "hebrew";
  lexicon?: GetChapterOriginalsResponse["lexicon"];
  /** original word ids to highlight (linked to the hovered German word or original word) */
  linkedIds: ReadonlySet<number>;
  onHoverOriginal: (verse: number, id: number | undefined) => void;
}

/** The side panel content of one verse: its TSK references or its source text. Memoised, the page re-renders on every hover. */
export const VerseSide = memo(function VerseSide({
  mode,
  verse,
  bookId,
  chapter,
  level,
  reference,
  books,
  references,
  onlyHighlighted = false,
  showHidden = false,
  editable = false,
  suggest = false,
  link,
  linkSelected,
  flashKeys,
  onHoverReference,
  original,
  language,
  lexicon,
  linkedIds,
  onHoverOriginal,
}: VerseSideProps) {
  const { t } = useTranslation();
  const preview = useMemo(
    () => ({ bookId, chapter, verse, level }),
    [bookId, chapter, verse, level],
  );

  const listLink = useMemo<CrossReferenceLinkMode | undefined>(
    () =>
      link && {
        selected: linkSelected ?? NO_KEYS,
        dragging: link.dragging,
        onSelectedChange: (keys) => link.onSelectedChange(verse, keys),
        onDragStart: (references) => link.onDragStart(verse, references),
        onDragEnd: link.onDragEnd,
      },
    [link, linkSelected, verse],
  );

  const hoverReference = useMemo(
    () => onHoverReference && ((r: CrossReference | undefined) => onHoverReference(verse, r)),
    [onHoverReference, verse],
  );

  const shown = useMemo(
    () =>
      references?.filter((r) =>
        onlyHighlighted ? r.rating === "highlight" : showHidden || r.rating !== "hide",
      ),
    [references, onlyHighlighted, showHidden],
  );

  if (mode === "tsk") {
    // curators in curate mode get the panel on every verse, so a link can be added where TSK has none
    if ((!shown || shown.length === 0) && !(editable && showHidden)) return null;
    return (
      <VerseSidePanel mode="tsk" label={t("bible.sidePanel.verseTsk", { verse })}>
        <CrossReferenceList
          variant="inline"
          references={shown ?? []}
          books={books}
          preview={preview}
          showHidden={showHidden}
          editable={editable}
          suggest={suggest}
          link={listLink}
          flashKeys={flashKeys}
          onHoverReference={hoverReference}
        />
      </VerseSidePanel>
    );
  }

  return (
    <VerseSidePanel mode="original" label={t("bible.sidePanel.verseOriginal", { verse })}>
      {original && original.words.length > 0 ? (
        <OriginalVerse
          variant="margin"
          data={{ language, originalWords: original.words }}
          lexicon={lexicon}
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
