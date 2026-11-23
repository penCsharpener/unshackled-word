import { type QueryClient, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { bibleApi } from "./api";
import { sidePanelKeys } from "./sidePanel";
import type {
  AddTskCurationRequest,
  CrossReference,
  DeleteAddedTskCurationRequest,
  DeleteTskWordLinkRequest,
  SaveTskCurationRequest,
  SaveTskWordLinkRequest,
  TskLinkKey,
  TskProposal,
  TskProposalSubmitted,
  TskProposalsSubmitted,
  TskRating,
} from "./types";
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

/** The key of an added link for `DELETE tsk/curation/added` (end only for ranges). */
export function addedKey(
  source: { bookId: number; chapter: number; verse: number },
  target: CrossReference,
): DeleteAddedTskCurationRequest {
  return {
    sourceHebRefId: hebRefId(source.bookId, source.chapter, source.verse),
    targetStartHebRefId: hebRefId(target.bibleBookId, target.chapter, target.verse),
    ...(target.endVerse
      ? { targetEndHebRefId: hebRefId(target.bibleBookId, target.chapter, target.endVerse) }
      : {}),
  };
}

/** Key of a target within its source verse (end only for ranges), as the word link endpoints take it. */
export function linkKey(target: CrossReference): TskLinkKey {
  return {
    targetStartHebRefId: hebRefId(target.bibleBookId, target.chapter, target.verse),
    ...(target.endVerse
      ? { targetEndHebRefId: hebRefId(target.bibleBookId, target.chapter, target.endVerse) }
      : {}),
  };
}

/**
 * `PUT tsk/curation/word` (roadmap 0024.5): the targets linked to the Elb1871 words `from..to` of the source verse
 * (positions in either order).
 */
export function wordLinkRequest(
  source: { bookId: number; chapter: number; verse: number },
  targets: CrossReference[],
  from: number,
  to: number,
): SaveTskWordLinkRequest {
  return {
    sourceHebRefId: hebRefId(source.bookId, source.chapter, source.verse),
    keys: targets.map(linkKey),
    startPosition: Math.min(from, to),
    endPosition: Math.max(from, to),
  };
}

/** `DELETE tsk/curation/word`: the word link of these targets is removed. */
export function wordUnlinkRequest(
  source: { bookId: number; chapter: number; verse: number },
  targets: CrossReference[],
): DeleteTskWordLinkRequest {
  return {
    sourceHebRefId: hebRefId(source.bookId, source.chapter, source.verse),
    keys: targets.map(linkKey),
  };
}

/** The body of `POST tsk/curation/added`: reference and comment trimmed, empty comment → `null`. */
export function addRequest(
  source: { bookId: number; chapter: number; verse: number },
  reference: string,
  comment: string,
): AddTskCurationRequest {
  const trimmed = comment.trim();
  return {
    sourceHebRefId: hebRefId(source.bookId, source.chapter, source.verse),
    reference: reference.trim(),
    comment: trimmed === "" ? null : trimmed,
  };
}

/** Query keys of the suggestion lists (roadmap 0024.6f). */
export const tskProposalKeys = {
  mine: ["tsk", "proposals", "mine"] as const,
  review: ["tsk", "proposals", "review"] as const,
};

/**
 * True when a write answered 202 with a suggestion (`{ proposal }` or `{ proposals }`): the caller is no maintainer,
 * nothing changed yet.
 */
export function isProposal(
  result: unknown,
): result is TskProposalSubmitted | TskProposalsSubmitted {
  return (
    typeof result === "object" && result !== null && ("proposal" in result || "proposals" in result)
  );
}

/** Same key as `curationRequest` / `addedKey` builds: source and target HebRefIds (end only for ranges). */
export function sameKey(
  a: { sourceHebRefId: number; targetStartHebRefId: number; targetEndHebRefId?: number },
  b: { sourceHebRefId: number; targetStartHebRefId: number; targetEndHebRefId?: number },
) {
  return (
    a.sourceHebRefId === b.sourceHebRefId &&
    a.targetStartHebRefId === b.targetStartHebRefId &&
    (a.targetEndHebRefId ?? 0) === (b.targetEndHebRefId ?? 0)
  );
}

/** The caller's suggestions (`GET tsk/proposals/mine`), newest first; only fetched when `enabled`. */
export function useMyTskProposals(enabled: boolean) {
  return useQuery({
    queryKey: tskProposalKeys.mine,
    queryFn: ({ signal }) => bibleApi.myTskProposals(signal),
    enabled,
  });
}

/**
 * The newest decision on a link when it is a rejection (the editor shows "Abgelehnt: …"): the caller's newest
 * suggestion for that key, if it was rejected; a later pending or accepted one hides the old reason.
 */
export function latestRejection(
  proposals: TskProposal[] | undefined,
  key: { sourceHebRefId: number; targetStartHebRefId: number; targetEndHebRefId?: number },
): TskProposal | undefined {
  const latest = proposals?.find((p) => sameKey(p, key) && p.state !== "superseded");
  return latest?.state === "rejected" ? latest : undefined;
}

/**
 * After any curation write the chapter lists (both with and without hidden links, every level), the hover texts and
 * the word boxes are refetched, since all of them carry rating, comment, origin, word link and order.
 */
export function invalidateTskViews(queryClient: QueryClient) {
  return Promise.all([
    queryClient.invalidateQueries({ queryKey: sidePanelKeys.crossReferencesAll }),
    queryClient.invalidateQueries({ queryKey: crossReferenceTextsAll }),
    queryClient.invalidateQueries({ queryKey: wordDetailKeys.all }),
    queryClient.invalidateQueries({ queryKey: ["tsk", "proposals"] }),
  ]);
}

function useInvalidateTsk() {
  const queryClient = useQueryClient();
  return () => invalidateTskViews(queryClient);
}

/** Maintainer review (`/edit/tsk`): the pending suggestions. */
export function useTskReviewProposals(enabled: boolean) {
  return useQuery({
    queryKey: tskProposalKeys.review,
    queryFn: ({ signal }) => bibleApi.tskReviewProposals(signal),
    enabled,
  });
}

/** Maintainer: accept (optionally `force` over a stale base); reloads the list and every TSK view. */
export function useAcceptTskProposal() {
  const invalidate = useInvalidateTsk();
  return useMutation({
    mutationFn: ({ id, force }: { id: number; force: boolean }) =>
      bibleApi.acceptTskProposal(id, force),
    onSuccess: invalidate,
  });
}

/** Maintainer: reject with an optional reason; reloads the list and every TSK view. */
export function useRejectTskProposal() {
  const invalidate = useInvalidateTsk();
  return useMutation({
    mutationFn: ({ id, reason }: { id: number; reason?: string }) =>
      bibleApi.rejectTskProposal(id, reason),
    onSuccess: invalidate,
  });
}

/** Maintainer save of one link's rating and comment (`PUT tsk/curation`). */
export function useSaveTskCuration() {
  const invalidate = useInvalidateTsk();
  return useMutation({
    mutationFn: (body: SaveTskCurationRequest) => bibleApi.saveTskCuration(body),
    onSuccess: invalidate,
  });
}

/** Maintainer: adds a link (`POST tsk/curation/added`, roadmap 0024.4f); same invalidations as a save. */
export function useAddTskCuration() {
  const invalidate = useInvalidateTsk();
  return useMutation({
    mutationFn: (body: AddTskCurationRequest) => bibleApi.addTskCuration(body),
    onSuccess: invalidate,
  });
}

/** Maintainer: removes an added link (`DELETE tsk/curation/added`); same invalidations as a save. */
export function useDeleteAddedTskCuration() {
  const invalidate = useInvalidateTsk();
  return useMutation({
    mutationFn: (body: DeleteAddedTskCurationRequest) => bibleApi.deleteAddedTskCuration(body),
    onSuccess: invalidate,
  });
}

/** Maintainer: links refs to a word range of their source verse (`PUT tsk/curation/word`); same invalidations. */
export function useLinkTskWord() {
  const invalidate = useInvalidateTsk();
  return useMutation({
    mutationFn: (body: SaveTskWordLinkRequest) => bibleApi.saveTskWordLink(body),
    onSuccess: invalidate,
  });
}

/** Maintainer: removes the word link of refs (`DELETE tsk/curation/word`); same invalidations. */
export function useUnlinkTskWord() {
  const invalidate = useInvalidateTsk();
  return useMutation({
    mutationFn: (body: DeleteTskWordLinkRequest) => bibleApi.deleteTskWordLink(body),
    onSuccess: invalidate,
  });
}
