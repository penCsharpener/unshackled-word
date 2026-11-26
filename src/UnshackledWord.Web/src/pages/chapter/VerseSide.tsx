import { memo, useMemo } from "react";
import { useTranslation } from "react-i18next";
import { type CrossReferenceLinkMode, CrossReferenceList } from "@/components/CrossReferenceList";
import { OriginalVerse } from "@/components/OriginalVerse";
import { SimilarVerseList } from "@/components/SimilarVerseList";
import { VerseSidePanel } from "@/components/VerseSidePanel";
import {
  type LxxForm,
  lxxBlocks,
  lxxOriginalWords,
  lxxReference,
  unitsOfForm,
} from "@/lib/bible/lxx";
import type {
  BibleStructureBook,
  ChapterOriginalVerse,
  CrossReference,
  GetChapterOriginalsResponse,
  LxxElbVerse,
  SimilarVerseHit,
  VerseSimilarVerses,
} from "@/lib/bible/types";

const NO_KEYS: ReadonlySet<string> = new Set();

export interface VerseLinkMode {
  dragging: boolean;
  onSelectedChange: (verse: number, keys: string[]) => void;
  onDragStart: (verse: number, references: CrossReference[]) => void;
  onDragEnd: () => void;
}

export interface VerseSideProps {
  mode: "tsk" | "original" | "lxx" | "similar";
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
  /** `original`: edition filter of the Grundtext panel (null = all words) */
  edition?: string | null;
  /** `similar`: the verse with its similar verses (absent = not in the response, shown as empty) */
  similar?: VerseSimilarVerses;
  /** `similar`: a hit is hovered or focused (`undefined` = none); the reader marks its source words in the verse. */
  onHoverSimilar?: (verse: number, hit: SimilarVerseHit | undefined) => void;
  /** `lxx` (roadmap 0028.4f): the verse's Septuagint units in reading order (absent or empty = not in the LXX) */
  lxx?: LxxElbVerse;
  /** `lxx`, Daniel: the text form to show (units without a form always show) */
  lxxForm?: LxxForm;
}

/**
 * The side panel content of one verse: its TSK references, its source text or its similar verses. Memoised, the page
 * re-renders on every hover.
 */
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
  edition = null,
  similar,
  onHoverSimilar,
  lxx,
  lxxForm = "th",
}: VerseSideProps) {
  const { t, i18n } = useTranslation();
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

  const hoverSimilar = useMemo(
    () => onHoverSimilar && ((hit: SimilarVerseHit | undefined) => onHoverSimilar(verse, hit)),
    [onHoverSimilar, verse],
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

  if (mode === "similar") {
    return (
      <VerseSidePanel mode="similar" label={t("bible.sidePanel.verseSimilar", { verse })}>
        <SimilarVerseList
          source={similar ?? { hebRefId: 0, bibleBookId: bookId, chapter, verse, hits: [] }}
          books={books}
          onHoverHit={hoverSimilar}
        />
      </VerseSidePanel>
    );
  }

  if (mode === "lxx") {
    const units = unitsOfForm(lxx?.units ?? [], lxxForm);
    return (
      <VerseSidePanel mode="lxx" label={t("bible.sidePanel.verseLxx", { verse })}>
        {units.length === 0 ? (
          <p className="text-fg-muted">{t("bible.sidePanel.lxx.absent")}</p>
        ) : (
          <div className="flex flex-col gap-2">
            {lxxBlocks(units).map((block) =>
              block.kind === "addition" ? (
                <section
                  key={block.units[0]?.lxxVerseId}
                  aria-label={t("bible.sidePanel.lxx.addition")}
                  className="rounded-md border border-border bg-surface-muted px-3 py-2"
                >
                  <h3 className="text-xs font-medium text-fg-muted">
                    {t("bible.sidePanel.lxx.addition")}
                  </h3>
                  <OriginalVerse
                    variant="margin"
                    data={{ language: "lxx", originalWords: lxxOriginalWords(block.units) }}
                    reference={reference}
                  />
                </section>
              ) : (
                <div key={block.unit.lxxVerseId}>
                  {block.unit.differsFromHebrew && (
                    <p className="text-xs text-fg-muted">
                      {t("bible.sidePanel.lxx.label", {
                        reference: lxxReference(block.unit, books, i18n.language),
                      })}
                    </p>
                  )}
                  <OriginalVerse
                    variant="margin"
                    data={{ language: "lxx", originalWords: lxxOriginalWords([block.unit]) }}
                    reference={reference}
                  />
                </div>
              ),
            )}
          </div>
        )}
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
          edition={edition}
        />
      ) : (
        <p className="text-fg-muted">{t("bible.sidePanel.noOriginal")}</p>
      )}
    </VerseSidePanel>
  );
});
