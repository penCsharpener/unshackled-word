import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useAuth } from "@/lib/auth/AuthContext";
import { type ChapterNotesResponse, type Note, type NoteAnchor, notesApi } from "./api";

export const notesKeys = {
  /** Prefix of every cached chapter note list, invalidated after each note change. */
  chapters: ["notes", "chapter"] as const,
  /** The list depends on who asks (own private notes), so the viewer is part of the key. */
  chapter: (bookId: number, chapter: number, viewer: number | undefined) =>
    ["notes", "chapter", bookId, chapter, viewer ?? 0] as const,
};

const NO_NOTES: Note[] = [];
const notesOf = (data: ChapterNotesResponse | undefined) =>
  Array.isArray(data?.notes) ? data.notes : NO_NOTES;

/** Notes of a chapter the viewer may see, in reading order (chapter notes first). */
export function useChapterNotes(ref: { bookId: number; chapter: number } | null) {
  const { status, user } = useAuth();
  return useQuery({
    queryKey: notesKeys.chapter(ref?.bookId ?? 0, ref?.chapter ?? 0, user?.userId),
    queryFn: ({ signal }) => notesApi.chapter(ref?.bookId ?? 0, ref?.chapter ?? 0, signal),
    select: notesOf,
    // wait for the session, else the anonymous list is fetched and thrown away right after
    enabled: ref !== null && status !== "loading",
  });
}

function useInvalidateNotes() {
  const queryClient = useQueryClient();
  return () => queryClient.invalidateQueries({ queryKey: notesKeys.chapters });
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
    mutationFn: ({ id, bodyMarkdown }: { id: string; bodyMarkdown: string }) =>
      notesApi.update(id, bodyMarkdown),
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
