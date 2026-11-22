import { useMutation } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { Link, Navigate, useSearchParams } from "react-router";
import { Button } from "@/components/Button";
import { EmptyState } from "@/components/EmptyState";
import { FormError } from "@/components/FormError";
import { Select } from "@/components/Select";
import { StatusBadge, type StatusTone } from "@/components/StatusBadge";
import { TextField } from "@/components/TextField";
import { describeApiError } from "@/lib/api";
import { useAuth } from "@/lib/auth/AuthContext";
import { bookName } from "@/lib/bible/books";
import { useBibleStructure } from "@/lib/bible/queries";
import type { BibleStructureBook } from "@/lib/bible/types";
import { saveBlob } from "@/lib/download";
import { formatStamp } from "@/lib/format";
import {
  MY_NOTES_PAGE_SIZE,
  type MyNotesQuery,
  type Note,
  type NoteVisibility,
  noteEndVerse,
  noteExcerpt,
  noteHref,
  notesApi,
  noteVerse,
} from "@/lib/notes/api";
import { useMyNotes } from "@/lib/notes/queries";
import { useDebouncedValue } from "@/lib/useDebouncedValue";

const VISIBILITIES: NoteVisibility[] = ["private", "internal", "public"];

function parsePage(value: string | null): number {
  const page = Number(value);
  return Number.isInteger(page) && page > 0 ? page : 1;
}

function parseVisibility(value: string | null): NoteVisibility | undefined {
  return VISIBILITIES.find((item) => item === value);
}

function parseBook(value: string | null): number | undefined {
  const book = Number(value);
  return Number.isInteger(book) && book >= 1 && book <= 66 ? book : undefined;
}

/** "Römer 8", "Römer 8,28" or "Römer 8,28–30" in the UI language; the book id until the Bible structure has loaded. */
function referenceOf(
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

/** Visibility of a note as a badge; a public note also says where it is in the review. */
function visibilityBadge(note: Note): { tone: StatusTone; key: string } {
  if (note.visibility === "private") return { tone: "neutral", key: "notes.mine.private" };
  if (note.visibility === "internal") return { tone: "info", key: "notes.mine.internal" };
  if (note.publicState === "approved") return { tone: "ok", key: "notes.mine.publicApproved" };
  if (note.publicState === "rejected") return { tone: "error", key: "notes.mine.publicRejected" };
  return { tone: "warning", key: "notes.mine.publicPending" };
}

/**
 * "Meine Notizen" (`/notes`): the signed-in user's own notes, filtered by book and visibility, searched in the text and paged.
 * Collapsed rows show a plain-text excerpt (no editor per row); the reference links to the reader with the note opened.
 */
export function MyNotesPage() {
  const { t, i18n } = useTranslation();
  const { status } = useAuth();
  const [params, setParams] = useSearchParams();
  const structure = useBibleStructure();

  const book = parseBook(params.get("book"));
  const visibility = parseVisibility(params.get("visibility"));
  const query = params.get("q") ?? "";
  const page = parsePage(params.get("page"));
  const skip = (page - 1) * MY_NOTES_PAGE_SIZE;

  const [search, setSearch] = useState(query);
  const debouncedSearch = useDebouncedValue(search, 300);

  function updateParams(changes: Record<string, string | null>, replace = false) {
    setParams(
      (prev) => {
        const next = new URLSearchParams(prev);
        for (const [key, value] of Object.entries(changes)) {
          if (value === null) next.delete(key);
          else next.set(key, value);
        }
        return next;
      },
      { replace },
    );
  }

  // Only a changed search text writes to the URL (and starts again at page 1); the URL changing must not write back.
  // biome-ignore lint/correctness/useExhaustiveDependencies: keyed on the debounced search text on purpose
  useEffect(() => {
    const term = debouncedSearch.trim();
    if (term === query) return;
    updateParams({ q: term || null, page: null }, true);
  }, [debouncedSearch]);

  const listQuery: MyNotesQuery = {
    book,
    visibility,
    q: query || undefined,
    skip,
    take: MY_NOTES_PAGE_SIZE,
  };
  const list = useMyNotes(listQuery);
  const download = useMutation({
    mutationFn: notesApi.exportMine,
    onSuccess: ({ blob, fileName }) => saveBlob(blob, fileName),
  });

  if (status === "loading") return null;
  if (status === "anonymous") return <Navigate to="/login" replace />;

  const number = (value: number) => value.toLocaleString(i18n.language);
  const data = list.data;
  const last = data ? skip + data.notes.length : 0;
  const books = structure.data;

  return (
    <section className="mx-auto flex w-full max-w-4xl flex-col gap-4 p-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-2xl font-semibold">{t("notes.mine.title")}</h1>
        <Button
          variant="secondary"
          size="sm"
          disabled={download.isPending}
          onClick={() => download.mutate()}
        >
          {t("notes.mine.download")}
        </Button>
      </div>
      <FormError
        message={
          download.isError ? describeApiError(download.error, t("notes.mine.downloadError")) : ""
        }
      />

      <div className="flex flex-wrap items-end gap-3">
        <TextField
          type="search"
          required={false}
          size="sm"
          label={t("notes.mine.search")}
          value={search}
          onChange={setSearch}
        />
        <Select
          label={t("notes.mine.book")}
          value={book ? String(book) : ""}
          onChange={(value) => updateParams({ book: value || null, page: null })}
          options={[
            { value: "", label: t("notes.mine.allBooks") },
            ...(books ?? []).map((item) => ({
              value: String(item.id),
              label: bookName(item, i18n.language),
            })),
          ]}
        />
        <Select
          label={t("notes.mine.visibility")}
          value={visibility ?? ""}
          onChange={(value) => updateParams({ visibility: value || null, page: null })}
          options={[
            { value: "", label: t("notes.mine.allVisibilities") },
            ...VISIBILITIES.map((item) => ({ value: item, label: t(`notes.mine.${item}`) })),
          ]}
        />
        {data ? (
          <p className="ml-auto text-sm text-fg-muted tabular-nums">
            {t("notes.mine.count", { count: data.total, formatted: number(data.total) })}
          </p>
        ) : null}
      </div>

      <FormError message={list.isError ? t("notes.mine.loadError") : ""} />
      {data && data.notes.length === 0 ? (
        <EmptyState>
          {book || visibility || query ? t("notes.mine.noMatch") : t("notes.mine.empty")}
        </EmptyState>
      ) : null}
      {data && data.notes.length > 0 ? (
        <div className="grid gap-2">
          <ul className="grid gap-2">
            {data.notes.map((note) => {
              const badge = visibilityBadge(note);
              const reference = referenceOf(note, books, i18n.language);
              return (
                <li
                  key={note.id}
                  data-note-id={note.id}
                  className="grid gap-1 rounded-md border border-border bg-surface p-3"
                >
                  <div className="flex flex-wrap items-center justify-between gap-2">
                    <Link to={noteHref(note)} className="font-medium text-accent hover:underline">
                      {note.anchor.kind === "words" && note.anchor.quotedText
                        ? t("notes.anchorWords", { quote: note.anchor.quotedText, reference })
                        : reference}
                    </Link>
                    <StatusBadge tone={badge.tone}>{t(badge.key)}</StatusBadge>
                  </div>
                  <p className="text-sm">{noteExcerpt(note.bodyLinked)}</p>
                  <p className="text-xs text-fg-muted">
                    {formatStamp(note.updatedAt ?? note.createdAt, i18n.language)}
                  </p>
                </li>
              );
            })}
          </ul>
          <div className="flex items-center justify-between gap-3 text-sm text-fg-muted">
            <span className="tabular-nums">
              {t("notes.mine.range", {
                from: number(skip + 1),
                to: number(last),
                total: number(data.total),
              })}
            </span>
            <div className="flex gap-2">
              <Button
                variant="secondary"
                size="sm"
                disabled={page <= 1}
                onClick={() => updateParams({ page: page - 1 <= 1 ? null : String(page - 1) })}
              >
                {t("notes.mine.prev")}
              </Button>
              <Button
                variant="secondary"
                size="sm"
                disabled={last >= data.total}
                onClick={() => updateParams({ page: String(page + 1) })}
              >
                {t("notes.mine.next")}
              </Button>
            </div>
          </div>
        </div>
      ) : null}
    </section>
  );
}
