import { useMutation, useQueryClient } from "@tanstack/react-query";
import { bibleApi } from "./api";
import { sidePanelKeys } from "./sidePanel";
import type { CrossReference, SaveTskCurationRequest, TskRating } from "./types";
import { wordDetailKeys } from "./wordDetail";

/** Maximum length of a curator comment (server validation: 500 after trimming). */
export const TSK_COMMENT_MAX = 500;

/** Prefix of every cached verse cross reference text list (hover cards). */
const crossReferenceTextsAll = ["bible", "crossReferenceTexts"] as const;

/** HebRefId of a verse in Elberfelder numbering: `BBCCCVVV` (1Mo 1,1 = 1001001). */
export const hebRefId = (bookId: number, chapter: number, verse: number) =>
  bookId * 1_000_000 + chapter * 1_000 + verse;

/** The request body for one link: key from the source verse and the target, comment trimmed (empty → `null`). */
export function curationRequest(
  source: { bookId: number; chapter: number; verse: number },
  target: CrossReference,
  rating: TskRating | null,
  comment: string,
): SaveTskCurationRequest {
  const trimmed = comment.trim();
  return {
    sourceHebRefId: hebRefId(source.bookId, source.chapter, source.verse),
    targetStartHebRefId: hebRefId(target.bibleBookId, target.chapter, target.verse),
    ...(target.endVerse
      ? { targetEndHebRefId: hebRefId(target.bibleBookId, target.chapter, target.endVerse) }
      : {}),
    rating,
    comment: trimmed === "" ? null : trimmed,
  };
}

/**
 * Maintainer save of one link's rating and comment. Afterwards the chapter lists (both with and without hidden
 * links), the hover texts and the word boxes are refetched, since all of them carry rating, comment and order.
 */
export function useSaveTskCuration() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: SaveTskCurationRequest) => bibleApi.saveTskCuration(body),
    onSuccess: () =>
      Promise.all([
        queryClient.invalidateQueries({ queryKey: sidePanelKeys.crossReferencesAll }),
        queryClient.invalidateQueries({ queryKey: crossReferenceTextsAll }),
        queryClient.invalidateQueries({ queryKey: wordDetailKeys.all }),
      ]),
  });
}
