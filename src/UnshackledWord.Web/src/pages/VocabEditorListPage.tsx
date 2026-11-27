import { useQuery } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { Navigate, useSearchParams } from "react-router";
import { Button } from "@/components/Button";
import { EmptyState } from "@/components/EmptyState";
import { FormError } from "@/components/FormError";
import { Select } from "@/components/Select";
import { TextField } from "@/components/TextField";
import { VocabEntryList, type VocabEntryRow } from "@/components/VocabEntryList";
import { VocabFamilyFilter } from "@/components/VocabFamilyFilter";
import { useAuth } from "@/lib/auth/AuthContext";
import { hasWriteRole } from "@/lib/auth/roles";
import { useDebouncedValue } from "@/lib/useDebouncedValue";
import { getCourses, VOCAB_PAGE_SIZE, type VocabTrack, vocabLang } from "@/lib/vocab/api";
import {
  getVocabEditorList,
  type VocabEditorListQuery,
  vocabEditorKeys,
} from "@/lib/vocab/editorApi";

const TRACKS: VocabTrack[] = ["G", "H"];

/** Filters of the editor list; `all` sends no flag. The default is "Ungeprüft" (unreviewed). */
const FILTERS = [
  "unreviewed",
  "noAudio",
  "formMissing",
  "formFromAgent",
  "multiKey",
  "retired",
  "missingRoot",
  "all",
] as const;
type EditorFilter = (typeof FILTERS)[number];

const FILTER_FLAGS: Record<EditorFilter, Partial<VocabEditorListQuery>> = {
  all: {},
  unreviewed: { unreviewed: true },
  noAudio: { noAudio: true },
  formMissing: { formMissing: true },
  formFromAgent: { formFromAgent: true },
  multiKey: { multiKey: true },
  retired: { retired: true },
  missingRoot: { missingRoot: true },
};

/** The family filter from the URL: a positive root id, otherwise none. */
function parseRoot(value: string | null): number | null {
  const id = Number(value);
  return value !== null && Number.isInteger(id) && id > 0 ? id : null;
}

function parsePage(value: string | null): number {
  const page = Number(value);
  return Number.isInteger(page) && page > 0 ? page : 1;
}

function parseFilter(value: string | null): EditorFilter {
  return FILTERS.find((filter) => filter === value) ?? "unreviewed";
}

/** A rank bound from the URL: absent = the default, empty or not a positive number = no bound. */
function parseRank(value: string | null): number | null {
  if (value === null) return null;
  const rank = Number(value);
  return value.trim() !== "" && Number.isInteger(rank) && rank > 0 ? rank : null;
}

/**
 * Vocab editor list (screen 5, `/edit/vocab`): one course, filters and rank range in the URL, paged.
 * Admin and Maintainer only.
 */
export function VocabEditorListPage() {
  const { t, i18n } = useTranslation();
  const [params, setParams] = useSearchParams();
  const { status, user } = useAuth();
  const canWrite = hasWriteRole(user);

  const track: VocabTrack = params.get("track") === "H" ? "H" : "G";
  const filter = parseFilter(params.get("filter"));
  const page = parsePage(params.get("page"));
  const query = params.get("q") ?? "";
  const root = parseRoot(params.get("root"));
  const subRoots = params.get("subRoots") !== "false";
  const rankFromText = params.get("rankFrom") ?? "";
  const rankToText = params.get("rankTo") ?? "";
  const rankFrom = parseRank(params.get("rankFrom"));
  const rankTo = parseRank(params.get("rankTo"));
  const skip = (page - 1) * VOCAB_PAGE_SIZE;

  const [search, setSearch] = useState(query);
  const debouncedSearch = useDebouncedValue(search, 300);

  const number = (value: number) => value.toLocaleString(i18n.language);

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

  // A link that changes the search (the root's Strong's link, back/forward) updates the search field.
  // biome-ignore lint/correctness/useExhaustiveDependencies: keyed on the URL query on purpose
  useEffect(() => {
    if (query !== debouncedSearch.trim()) setSearch(query);
  }, [query]);

  const courses = useQuery({
    queryKey: ["vocab", "courses"],
    queryFn: ({ signal }) => getCourses(signal),
    enabled: canWrite,
  });
  const courseCode = courses.data?.courses.find((item) => item.track === track)?.code;

  const listQuery: VocabEditorListQuery = {
    course: courseCode,
    q: query,
    ...FILTER_FLAGS[filter],
    rankFrom: rankFrom ?? undefined,
    rankTo: rankTo ?? undefined,
    root: root ?? undefined,
    subRoots,
    skip,
    take: VOCAB_PAGE_SIZE,
  };
  const list = useQuery({
    queryKey: vocabEditorKeys.list(listQuery),
    queryFn: ({ signal }) => getVocabEditorList(listQuery, signal),
    enabled: canWrite && courseCode !== undefined,
  });

  /** The list of one word family; a family shows every entry, so without an explicit filter the default "Ungeprüft" becomes "Alle". */
  const familyHref = (rootId: number) => {
    const next = new URLSearchParams(params);
    next.set("root", String(rootId));
    next.delete("page");
    next.delete("subRoots");
    if (!next.has("filter")) next.set("filter", "all");
    return `/edit/vocab?${next.toString()}`;
  };

  if (status === "loading") return null;
  if (status === "anonymous") return <Navigate to="/login" replace />;
  if (!canWrite) {
    return (
      <section className="mx-auto flex w-full max-w-6xl flex-col gap-4 p-4">
        <p role="alert" className="text-fg-muted">
          {t("admin.forbidden")}
        </p>
      </section>
    );
  }

  const data = list.data;
  const rows: VocabEntryRow[] = (data?.items ?? []).map((item) => ({
    id: item.id,
    rank: item.rank ?? null,
    lemma: item.lemma,
    transliteration: item.transliteration ?? "",
    dictionaryForm: item.dictionaryForm ?? null,
    meanings: item.meanings.map((meaning) => meaning.german),
    occurrences: item.occurrences,
    hasAudio: item.hasAudio,
    reviewed: item.reviewed,
    strongsKeys: item.strongsKeys,
    root: item.root ?? null,
    lang: vocabLang(item.track),
    formOrigin: item.dictionaryFormOrigin ?? null,
    learners: item.learners,
  }));
  const last = data ? skip + data.items.length : 0;
  const failed = courses.isError || list.isError;

  return (
    <section className="mx-auto flex w-full max-w-6xl flex-col gap-4 p-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-2xl font-semibold">{t("vocab.editor.title")}</h1>
        {/* biome-ignore lint/a11y/useSemanticElements: a segmented toggle of two buttons, not a form fieldset */}
        <div className="flex gap-1" role="group" aria-label={t("vocab.list.trackGroup")}>
          {TRACKS.map((item) => (
            <Button
              key={item}
              variant="secondary"
              size="sm"
              aria-pressed={track === item}
              onClick={() => updateParams({ track: item, page: null })}
            >
              {t(`vocab.editor.tracks.${item}`)}
            </Button>
          ))}
        </div>
      </div>

      <div className="flex flex-wrap items-end gap-3">
        <TextField
          type="search"
          required={false}
          size="sm"
          label={t("vocab.list.search")}
          placeholder={t("vocab.editor.searchPlaceholder")}
          value={search}
          onChange={setSearch}
        />
        <Select
          label={t("vocab.editor.filter")}
          value={filter}
          onChange={(value) => updateParams({ filter: value, page: null })}
          options={FILTERS.map((value) => ({ value, label: t(`vocab.editor.filters.${value}`) }))}
        />
        <TextField
          type="number"
          required={false}
          size="sm"
          label={t("vocab.editor.rankFrom")}
          value={rankFromText}
          onChange={(value) => updateParams({ rankFrom: value, page: null }, true)}
          className="w-24"
        />
        <TextField
          type="number"
          required={false}
          size="sm"
          label={t("vocab.editor.rankTo")}
          value={rankToText}
          onChange={(value) => updateParams({ rankTo: value, page: null }, true)}
          className="w-24"
        />
        {data ? (
          <p className="ml-auto text-sm text-fg-muted tabular-nums">
            {t("vocab.editor.hits", { count: number(data.total) })}
          </p>
        ) : null}
      </div>

      {root !== null ? (
        <VocabFamilyFilter
          rootId={root}
          lang={vocabLang(track)}
          subRoots={subRoots}
          onSubRootsChange={(value) =>
            updateParams({ subRoots: value ? null : "false", page: null })
          }
          onClear={() => updateParams({ root: null, subRoots: null, page: null })}
          rootHref={familyHref}
        />
      ) : null}
      <FormError message={failed ? t("vocab.editor.listLoadFailed") : ""} />
      {data && data.items.length === 0 ? <EmptyState>{t("vocab.editor.empty")}</EmptyState> : null}
      {data && data.items.length > 0 ? (
        <div className="grid gap-2">
          <VocabEntryList
            mode="editor"
            entries={rows}
            linkTo={(id) => `/edit/vocab/${id}`}
            searchTo={(q) => `/edit/vocab?track=${track}&q=${encodeURIComponent(q)}`}
            rootTo={familyHref}
          />
          <div className="flex items-center justify-between gap-3 text-sm text-fg-muted">
            <span className="tabular-nums">
              {t("vocab.list.range", {
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
                {t("vocab.list.prev")}
              </Button>
              <Button
                variant="secondary"
                size="sm"
                disabled={last >= data.total}
                onClick={() => updateParams({ page: String(page + 1) })}
              >
                {t("vocab.list.next")}
              </Button>
            </div>
          </div>
        </div>
      ) : null}
    </section>
  );
}
