import { api } from "@/lib/api";

/** Kinds that exist so far (roadmap 0023.1); `verses` and `words` follow in 0023.2. */
export type NoteKind = "chapter" | "verse";

/** Where a note sits; `HebRefId` = book * 1,000,000 + chapter * 1,000 + verse. */
export interface NoteAnchor {
  kind: NoteKind | "verses" | "words";
  bookId: number;
  chapter: number;
  startHebRefId?: number;
  endHebRefId?: number;
  startPosition?: number;
  endPosition?: number;
  /** response only */
  quotedText?: string;
  /** response only: verse number of `startHebRefId` */
  startVerse?: number;
  endVerse?: number;
}

export interface Note {
  id: string;
  anchor: NoteAnchor;
  anchorState: "ok" | "drifted" | "noBaseWords";
  bodyMarkdown: string;
  bodyLinked: string;
  visibility: "private" | "internal" | "public";
  publicState: "none" | "pending" | "approved" | "rejected";
  isOwn: boolean;
  isHidden?: boolean;
  authorDisplayName: string;
  createdAt: string;
  updatedAt?: string;
}

export interface ChapterNotesResponse {
  /** In reading order: chapter notes first, then by verse, then oldest first. */
  notes: Note[];
}

/** Anchor of a new note: the whole chapter or one verse of it. */
export type NoteTarget = { kind: "chapter" } | { kind: "verse"; verse: number };

export const hebRefId = (bookId: number, chapter: number, verse: number) =>
  bookId * 1_000_000 + chapter * 1_000 + verse;

export function anchorFor(bookId: number, chapter: number, target: NoteTarget): NoteAnchor {
  return target.kind === "chapter"
    ? { kind: "chapter", bookId, chapter }
    : { kind: "verse", bookId, chapter, startHebRefId: hebRefId(bookId, chapter, target.verse) };
}

/** Verse of a verse note (from the response field, else from the HebRefId); undefined for chapter notes. */
export function noteVerse(note: Note): number | undefined {
  if (note.anchor.kind === "chapter") return undefined;
  return note.anchor.startVerse ?? (note.anchor.startHebRefId ?? 0) % 1_000;
}

/** Plain-text preview of a Markdown body for a collapsed note: no markers, links reduced to their text, one line. */
export function noteExcerpt(markdown: string, max = 140): string {
  const text = markdown
    .replace(/\[([^\]]*)\]\([^)]*\)/g, "$1")
    .replace(/^\s{0,3}(?:#{1,6}\s+|>\s?|[-*+]\s+|\d+[.)]\s+)/gm, "")
    .replace(/[*_`~]+/g, "")
    .replace(/\s+/g, " ")
    .trim();
  return text.length > max ? `${text.slice(0, max - 1).trimEnd()}…` : text;
}

export const notesApi = {
  chapter: (bookId: number, chapter: number, signal?: AbortSignal) =>
    api.get<ChapterNotesResponse>(`/notes/chapter/${bookId}/${chapter}`, signal),
  create: (anchor: NoteAnchor, bodyMarkdown: string) =>
    api.post<Note>("/notes", { anchor, bodyMarkdown }),
  update: (id: string, bodyMarkdown: string) =>
    api.put<Note>(`/notes/${encodeURIComponent(id)}`, { bodyMarkdown }),
  remove: (id: string) => api.delete<void>(`/notes/${encodeURIComponent(id)}`),
};
