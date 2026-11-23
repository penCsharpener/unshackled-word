import type { StatusTone } from "@/components/StatusBadge";
import { bookName } from "@/lib/bible/books";
import type { BibleStructureBook } from "@/lib/bible/types";
import { type Note, noteEndVerse, noteVerse } from "./api";

/** The review facts of a note that its badge shows. */
export type NoteStatus = Pick<Note, "visibility" | "publicState" | "isHidden" | "reviewNote">;

/** Visibility of a note as a badge; a public note also says where it is in the review. */
export function visibilityBadge(note: NoteStatus): { tone: StatusTone; key: string } {
  if (note.visibility === "private") return { tone: "neutral", key: "notes.mine.private" };
  if (note.visibility === "internal") return { tone: "info", key: "notes.mine.internal" };
  if (note.publicState === "approved") return { tone: "ok", key: "notes.mine.publicApproved" };
  if (note.publicState === "rejected") return { tone: "error", key: "notes.mine.publicRejected" };
  return { tone: "warning", key: "notes.mine.publicPending" };
}

/** Badges of a note's state: the visibility (with the review state of a public note) and "ausgeblendet" for a hidden one. */
export function noteStatusBadges(note: NoteStatus): { tone: StatusTone; key: string }[] {
  const badges = [visibilityBadge(note)];
  if (note.isHidden) badges.push({ tone: "error", key: "notes.visibility.hidden" });
  return badges;
}

/** The maintainer's reason the author should read: why the note was hidden or rejected; undefined without one. */
export function noteStatusReason(note: NoteStatus): { key: string; text: string } | undefined {
  if (!note.reviewNote) return undefined;
  if (note.isHidden) return { key: "notes.visibility.hiddenReason", text: note.reviewNote };
  if (note.publicState === "rejected") {
    return { key: "notes.visibility.rejectedReason", text: note.reviewNote };
  }
  return undefined;
}

/** "Römer 8", "Römer 8,28" or "Römer 8,28–30" in the UI language; the book id until the Bible structure has loaded. */
export function noteReference(
  note: Note,
  books: readonly BibleStructureBook[] | undefined,
  language: string,
): string {
  const { bookId, chapter } = note.anchor;
  const book = books?.find((item) => item.id === bookId);
  const name = book ? bookName(book, language) : String(bookId);
  const verse = noteVerse(note);
  if (verse === undefined) return `${name} ${chapter}`;
  const end = noteEndVerse(note) ?? verse;
  return end > verse ? `${name} ${chapter},${verse}–${end}` : `${name} ${chapter},${verse}`;
}
