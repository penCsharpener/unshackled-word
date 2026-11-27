import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { useSearchParams } from "react-router";
import { Button } from "@/components/Button";
import { EmptyState } from "@/components/EmptyState";
import { FormError } from "@/components/FormError";
import { Select } from "@/components/Select";
import { TextField } from "@/components/TextField";
import { VocabEntryList, type VocabEntryRow } from "@/components/VocabEntryList";
import { VocabFamilyFilter } from "@/components/VocabFamilyFilter";
import { useDebouncedValue } from "@/lib/useDebouncedValue";
import {
  getCourseEntries,
  getCourses,
  VOCAB_PAGE_SIZE,
  type VocabCourse,
  type VocabEntriesResponse,
  type VocabTrack,
  vocabLang,
} from "@/lib/vocab/api";
import { useAudioPlayer } from "@/lib/vocab/useAudioPlayer";

const TRACKS: VocabTrack[] = ["G", "H"];

function parsePage(value: string | null): number {
  const page = Number(value);
  return Number.isInteger(page) && page > 0 ? page : 1;
}

/** The family filter from the URL: a positive root id, otherwise none. */
function parseRoot(value: string | null): number | null {
  const id = Number(value);
  return value !== null && Number.isInteger(id) && id > 0 ? id : null;
}

/** Public vocabulary list (screen 1): one course per testament, searchable and paged. Track, page and query live in the URL. */
export function VocabListPage() {
  const { t, i18n } = useTranslation();
  const [params, setParams] = useSearchParams();
  const track: VocabTrack = params.get("track") === "H" ? "H" : "G";
  const page = parsePage(params.get("page"));
  const query = params.get("q") ?? "";
  const root = parseRoot(params.get("root"));
  const subRoots = params.get("subRoots") !== "false";
  const skip = (page - 1) * VOCAB_PAGE_SIZE;

  const [search, setSearch] = useState(query);
  const debouncedSearch = useDebouncedValue(search, 300);
  const [courses, setCourses] = useState<VocabCourse[] | null>(null);
  const [coursesFailed, setCoursesFailed] = useState(false);
  const [data, setData] = useState<VocabEntriesResponse | null>(null);
  const [entriesFailed, setEntriesFailed] = useState(false);

  const number = (value: number) => value.toLocaleString(i18n.language);
  const player = useAudioPlayer();

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

  useEffect(() => {
    const controller = new AbortController();
    getCourses(controller.signal)
      .then((response) => setCourses(response.courses))
      .catch(() => {
        if (!controller.signal.aborted) setCoursesFailed(true);
      });
    return () => controller.abort();
  }, []);

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

  const course = courses?.find((item) => item.track === track);
  const courseCode = course?.code;

  useEffect(() => {
    if (!courseCode) return;
    const controller = new AbortController();
    setData(null);
    setEntriesFailed(false);
    getCourseEntries(
      courseCode,
      { skip, take: VOCAB_PAGE_SIZE, q: query, root: root ?? undefined, subRoots },
      controller.signal,
    )
      .then((response) => setData(response))
      .catch(() => {
        if (!controller.signal.aborted) setEntriesFailed(true);
      });
    return () => controller.abort();
  }, [courseCode, skip, query, root, subRoots]);

  const lang = vocabLang(track);

  /**
   * The list of one whole word family: only the track is kept. A search would cut the family down (the root's Strong's link
   * falls back to `?q=<root>`, and that search matches the root alone, not its compounds).
   */
  const familyHref = (rootId: number) => {
    const next = new URLSearchParams();
    const trackParam = params.get("track");
    if (trackParam) next.set("track", trackParam);
    next.set("root", String(rootId));
    return `/vocab?${next.toString()}`;
  };

  const rows: VocabEntryRow[] = data
    ? data.items.map((item) => ({
        ...item,
        meanings: item.meanings.map((meaning) => meaning.german),
        lang,
      }))
    : [];
  const last = data ? skip + data.items.length : 0;

  return (
    <section className="mx-auto flex w-full max-w-6xl flex-col gap-4 p-4">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div className="grid gap-1">
          <h1 className="text-2xl font-semibold">{t("vocab.list.title")}</h1>
          <p className="text-sm text-fg-muted">{t("vocab.list.intro")}</p>
        </div>
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
              {t(`vocab.list.tracks.${item}`)}
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
          placeholder={t("vocab.list.searchPlaceholder")}
          value={search}
          onChange={setSearch}
        />
        <Select
          label={t("vocab.list.show")}
          value="all"
          onChange={() => undefined}
          options={[
            { value: "all", label: t("vocab.list.showAll") },
            { value: "reviewed", label: t("vocab.list.showReviewed"), disabled: true },
            { value: "audio", label: t("vocab.list.showAudio"), disabled: true },
          ]}
        />
        {course ? (
          <p className="ml-auto text-sm text-fg-muted tabular-nums">
            {t("vocab.list.totals", {
              entries: number(course.totalEntries),
              occurrences: number(course.totalOccurrences),
            })}
          </p>
        ) : null}
      </div>
      {root !== null ? (
        <VocabFamilyFilter
          rootId={root}
          lang={lang}
          subRoots={subRoots}
          onSubRootsChange={(value) =>
            updateParams({ subRoots: value ? null : "false", page: null })
          }
          onClear={() => updateParams({ root: null, subRoots: null, page: null })}
          rootHref={familyHref}
        />
      ) : null}
      <FormError message={coursesFailed || entriesFailed ? t("vocab.list.loadFailed") : ""} />
      {data && data.items.length === 0 ? <EmptyState>{t("vocab.list.empty")}</EmptyState> : null}
      {data && data.items.length > 0 ? (
        <div className="grid gap-2">
          <VocabEntryList
            mode="public"
            entries={rows}
            linkTo={(id) => `/vocab/${id}`}
            searchTo={(q) => `/vocab?track=${track}&q=${encodeURIComponent(q)}`}
            rootTo={familyHref}
            onPlay={(id) => {
              const url = data?.items.find((item) => item.id === id)?.audioUrl;
              if (url) player.play(url);
            }}
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
