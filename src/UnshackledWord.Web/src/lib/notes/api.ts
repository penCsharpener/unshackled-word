import { api } from "@/lib/api";

export type NoteKind = "chapter" | "verse" | "verses" | "words";

/** Where a note sits; `HebRefId` = book * 1,000,000 + chapter * 1,000 + verse. */
export interface NoteAnchor {
  kind: NoteKind;
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
  /** `drifted`: the anchored words changed (verse level, quote as hint); `noBaseWords`: the shown variant has no word in the range (verse level). */
  anchorState: "ok" | "drifted" | "noBaseWords";
  /** Words notes on a verse with a variant at the requested level: the variant words that carry the marker. */
  mappedWords?: NoteMappedWord[];
  revisionVerseId?: number;
  bodyMarkdown: string;
  bodyLinked: string;
  visibility: "private" | "internal" | "public";
  publicState: "none" | "pending" | "approved" | "rejected";
  isOwn: boolean;
  isHidden?: boolean;
  /** Rejection or hide reason; only on the caller's own notes. */
  reviewNote?: string;
  authorDisplayName: string;
  createdAt: string;
  updatedAt?: string;
}

export interface NoteMappedWord {
  revisionWordId: number;
  positionInVerse: number;
  basePositionInVerse: number;
}

export interface ChapterNotesResponse {
  /** In reading order: chapter notes first, then by verse, then oldest first. */
  notes: Note[];
}

export type NoteVisibility = Note["visibility"];

export const NOTE_VISIBILITIES: readonly NoteVisibility[] = ["private", "internal", "public"];

/** One note of the maintainer's review queue (`GET /maintainer/notes/review`). */
export interface ReviewNote {
  /** current state; `bodyMarkdown` is the author's latest text */
  note: Note;
  /** what anonymous readers see; absent if never approved */
  approvedBodyMarkdown?: string;
  approvedAt?: string;
  hiddenAt?: string;
  /** rejection or hide reason */
  reviewNote?: string;
}

export interface ReviewNotesResponse {
  /** public notes waiting for approval, oldest change first */
  pending: ReviewNote[];
  /** hidden notes, oldest hide first */
  hidden: ReviewNote[];
}

export type ReviewAction = "approve" | "reject" | "hide" | "unhide";

/** Filters and paging of `GET /notes/mine`; every field is optional. */
export interface MyNotesQuery {
  book?: number;
  visibility?: NoteVisibility;
  q?: string;
  skip?: number;
  take?: number;
}

export const MY_NOTES_PAGE_SIZE = 50;

export interface MyNotesResponse {
  /** All notes matching the filters, not only this page. */
  total: number;
  /** Newest change first. */
  notes: Note[];
}

/** Anchor of a new note: the chapter, one verse, a verse range or words (Elb1871 positions) of one verse. */
export type NoteTarget =
  | { kind: "chapter" }
  | { kind: "verse"; verse: number }
  | { kind: "verses"; verse: number; endVerse: number }
  | { kind: "words"; verse: number; startPosition: number; endPosition: number; quote: string };

export const hebRefId = (bookId: number, chapter: number, verse: number) =>
  bookId * 1_000_000 + chapter * 1_000 + verse;

export function anchorFor(bookId: number, chapter: number, target: NoteTarget): NoteAnchor {
  if (target.kind === "chapter") return { kind: "chapter", bookId, chapter };
  const startHebRefId = hebRefId(bookId, chapter, target.verse);
  if (target.kind === "verses") {
    return {
      kind: "verses",
      bookId,
      chapter,
      startHebRefId,
      endHebRefId: hebRefId(bookId, chapter, target.endVerse),
    };
  }
  if (target.kind === "words") {
    return {
      kind: "words",
      bookId,
      chapter,
      startHebRefId,
      startPosition: target.startPosition,
      endPosition: target.endPosition,
    };
  }
  return { kind: "verse", bookId, chapter, startHebRefId };
}

/** Verse of a verse note (from the response field, else from the HebRefId); undefined for chapter notes. */
export function noteVerse(note: Note): number | undefined {
  if (note.anchor.kind === "chapter") return undefined;
  return note.anchor.startVerse ?? (note.anchor.startHebRefId ?? 0) % 1_000;
}

/** Last verse of a verse-range note; the start verse for every other verse-bound kind. */
export function noteEndVerse(note: Note): number | undefined {
  const start = noteVerse(note);
  if (start === undefined || note.anchor.kind !== "verses") return start;
  return (
    note.anchor.endVerse ?? (note.anchor.endHebRefId ? note.anchor.endHebRefId % 1_000 : start)
  );
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

/** Reader link of a note: the passage with its verses marked, the note opened in the notes panel and the first verse in view. */
export function noteHref(note: Note): string {
  const { bookId, chapter, kind } = note.anchor;
  const base = `/bible/${bookId}/${chapter}`;
  const verse = noteVerse(note);
  if (kind === "chapter" || verse === undefined) return `${base}?note=${note.id}`;
  const end = noteEndVerse(note) ?? verse;
  const range = end > verse ? `${verse}-${end}` : `${verse}`;
  return `${base}?v=${range}&note=${note.id}#v${verse}`;
}

export const notesApi = {
  /** `others = false`: only the caller's own notes. */
  chapter: (
    bookId: number,
    chapter: number,
    level: number,
    others = true,
    signal?: AbortSignal,
  ) => {
    const params = new URLSearchParams();
    if (level > 0) params.set("level", String(level));
    if (!others) params.set("others", "false");
    const query = params.size > 0 ? `?${params}` : "";
    return api.get<ChapterNotesResponse>(`/notes/chapter/${bookId}/${chapter}${query}`, signal);
  },
  mine: (query: MyNotesQuery, signal?: AbortSignal) => {
    const params = new URLSearchParams();
    if (query.book) params.set("book", String(query.book));
    if (query.visibility) params.set("visibility", query.visibility);
    if (query.q) params.set("q", query.q);
    params.set("skip", String(query.skip ?? 0));
    params.set("take", String(query.take ?? MY_NOTES_PAGE_SIZE));
    return api.get<MyNotesResponse>(`/notes/mine?${params}`, signal);
  },
  /** All own notes as the JSON file of the export format. */
  exportMine: () => api.download("/notes/mine/export", "notes.json"),
  create: (anchor: NoteAnchor, bodyMarkdown: string) =>
    api.post<Note>("/notes", { anchor, bodyMarkdown }),
  /** `anchor` re-anchors the note; omitted keeps it. */
  update: (id: string, bodyMarkdown: string, anchor?: NoteAnchor) =>
    api.put<Note>(`/notes/${encodeURIComponent(id)}`, {
      bodyMarkdown,
      ...(anchor && { anchor }),
    }),
  remove: (id: string) => api.delete<void>(`/notes/${encodeURIComponent(id)}`),
  /** `public` is a request: the note waits for a maintainer's approval. */
  setVisibility: (id: string, visibility: NoteVisibility) =>
    api.put<Note>(`/notes/${encodeURIComponent(id)}/visibility`, { visibility }),
  /** Maintainer: pending and hidden notes. */
  review: (signal?: AbortSignal) =>
    api.get<ReviewNotesResponse>("/maintainer/notes/review", signal),
  /** Maintainer action; `reason` only for reject and hide. */
  reviewAction: (id: string, action: ReviewAction, reason?: string) =>
    api.post<void>(
      `/maintainer/notes/${encodeURIComponent(id)}/${action}`,
      reason ? { reason } : {},
    ),
};
