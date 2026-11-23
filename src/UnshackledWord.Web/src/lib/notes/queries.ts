import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ApiError } from "@/lib/api";
import { useAuth } from "@/lib/auth/AuthContext";
import {
  type ChapterNotesResponse,
  type MyNotesQuery,
  type Note,
  type NoteAnchor,
  type NoteVisibility,
  notesApi,
  type ReviewAction,
} from "./api";

export const notesKeys = {
  /** Prefix of every cached chapter note list, invalidated after each note change. */
  chapters: ["notes", "chapter"] as const,
  /** Prefix of every "my notes" page; changing a note invalidates them too. */
  mine: ["notes", "mine"] as const,
  myPage: (viewer: number | undefined, query: MyNotesQuery) =>
    ["notes", "mine", viewer ?? 0, query] as const,
  /** The list depends on who asks (own private notes), so the viewer is part of the key. */
  chapter: (
    bookId: number,
    chapter: number,
    viewer: number | undefined,
    level: number,
    others: boolean,
  ) => ["notes", "chapter", bookId, chapter, viewer ?? 0, level, others] as const,
  /** Maintainer review queue. */
  review: ["notes", "review"] as const,
};

const NO_NOTES: Note[] = [];
const notesOf = (data: ChapterNotesResponse | undefined) =>
  Array.isArray(data?.notes) ? data.notes : NO_NOTES;

/**
 * Notes of a chapter the viewer may see, in reading order (chapter notes first). `level` is the reader's revision level, so
 * words notes on a revised verse come with the variant words that carry their marker.
 */
export function useChapterNotes(
  ref: { bookId: number; chapter: number } | null,
  level = 0,
  others = true,
) {
  const { status, user } = useAuth();
  return useQuery({
    queryKey: notesKeys.chapter(ref?.bookId ?? 0, ref?.chapter ?? 0, user?.userId, level, others),
    queryFn: ({ signal }) =>
      notesApi.chapter(ref?.bookId ?? 0, ref?.chapter ?? 0, level, others, signal),
    select: notesOf,
    // wait for the session, else the anonymous list is fetched and thrown away right after
    enabled: ref !== null && status !== "loading",
  });
}

/** One page of the signed-in user's own notes. */
export function useMyNotes(query: MyNotesQuery) {
  const { status, user } = useAuth();
  return useQuery({
    queryKey: notesKeys.myPage(user?.userId, query),
    queryFn: ({ signal }) => notesApi.mine(query, signal),
    enabled: status === "authenticated",
    placeholderData: keepPreviousData,
  });
}

function useInvalidateNotes() {
  const queryClient = useQueryClient();
  return () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: notesKeys.chapters }),
      queryClient.invalidateQueries({ queryKey: notesKeys.mine }),
      queryClient.invalidateQueries({ queryKey: notesKeys.review }),
    ]);
}

export function useCreateNote() {
  const invalidate = useInvalidateNotes();
  return useMutation({
    mutationFn: ({ anchor, bodyMarkdown }: { anchor: NoteAnchor; bodyMarkdown: string }) =>
      notesApi.create(anchor, bodyMarkdown),
    onSuccess: invalidate,
  });
}

export function useUpdateNote() {
  const invalidate = useInvalidateNotes();
  return useMutation({
    mutationFn: ({
      id,
      bodyMarkdown,
      anchor,
    }: {
      id: string;
      bodyMarkdown: string;
      anchor?: NoteAnchor;
    }) => notesApi.update(id, bodyMarkdown, anchor),
    onSuccess: invalidate,
  });
}

export function useDeleteNote() {
  const invalidate = useInvalidateNotes();
  return useMutation({
    mutationFn: (id: string) => notesApi.remove(id),
    onSuccess: invalidate,
  });
}

/** Owner: private / internal / public (a request for approval). */
export function useSetNoteVisibility() {
  const invalidate = useInvalidateNotes();
  return useMutation({
    mutationFn: ({ id, visibility }: { id: string; visibility: NoteVisibility }) =>
      notesApi.setVisibility(id, visibility),
    onSuccess: invalidate,
  });
}

/** Maintainer review queue: pending and hidden notes. */
export function useReviewNotes(enabled: boolean) {
  return useQuery({
    queryKey: notesKeys.review,
    queryFn: ({ signal }) => notesApi.review(signal),
    enabled,
  });
}

/**
 * Maintainer: approve (with the reviewed version), reject (reason), hide (reason) or unhide; reloads the queue and the
 * readers' lists, also after a 409 (the note changed since it was reviewed) so the new text shows up.
 */
export function useReviewAction() {
  const invalidate = useInvalidateNotes();
  return useMutation({
    mutationFn: ({
      id,
      action,
      reason,
      reviewedVersion,
    }: {
      id: string;
      action: ReviewAction;
      reason?: string;
      reviewedVersion?: string;
    }) => notesApi.reviewAction(id, action, reason, reviewedVersion),
    onSuccess: invalidate,
    onError: (error) => {
      if (error instanceof ApiError && error.status === 409) invalidate();
    },
  });
}
