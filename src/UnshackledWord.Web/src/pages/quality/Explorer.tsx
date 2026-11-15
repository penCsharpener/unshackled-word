import type { TFunction } from "i18next";
import { type FormEvent, useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { Button } from "@/components/Button";
import { EmptyState } from "@/components/EmptyState";
import { FindingsTable } from "@/components/FindingsTable";
import { RankedBarList, type RankedFlag } from "@/components/RankedBarList";
import { Select } from "@/components/Select";
import { TextField } from "@/components/TextField";
import type { BibleStructureBook } from "@/lib/bible/types";
import { formatNumber } from "@/lib/chart";
import {
  useGermanDetail,
  useGermanSearch,
  useHits,
  useOriginalDetail,
  useOriginalSearch,
} from "@/lib/quality/queries";
import { SECTION_IDS } from "@/lib/quality/scroll";
import type {
  GermanDetailResponse,
  OriginalDetailResponse,
  QualityFacets,
} from "@/lib/quality/types";
import {
  clearTrailPatch,
  type ExplorerSide,
  formatGermanQ,
  normalizeOriginalQ,
  parseGermanQ,
  type QualityPatch,
  type QualityUrlState,
  type TrailEntry,
  visibleCrumbs,
} from "@/lib/quality/url";
import { QueryState } from "./QueryState";
import { SectionHelp } from "./SectionHelp";
import { useJumps } from "./useJumps";

const DEBOUNCE_MS = 250;
const PHONE_CRUMBS = 3;
const HITS_PAGE_SIZE = 20;
/** Common German UPOS tags offered by the part-of-speech select. */
const POS_OPTIONS = [
  "NOUN",
  "VERB",
  "ADJ",
  "ADV",
  "PRON",
  "DET",
  "ADP",
  "CCONJ",
  "SCONJ",
  "PROPN",
  "NUM",
  "PART",
  "AUX",
  "INTJ",
];

const FLAG_SEVERITY: Record<string, RankedFlag["severity"]> = {
  minority: "info",
  posConflict: "warning",
  contentFunction: "warning",
  properNoun: "info",
};

const flagsOf = (t: TFunction, flags: string[]): RankedFlag[] =>
  flags.map((flag) => ({
    key: flag,
    label: t(`quality.flags.${flag}`, { defaultValue: flag }),
    severity: FLAG_SEVERITY[flag] ?? "info",
  }));

function crumbLabel(entry: TrailEntry): string {
  if (entry.side === "o") return entry.q;
  const { lemma, pos } = parseGermanQ(entry.q);
  return pos ? `${lemma} (${pos})` : lemma;
}

function Trail({
  trail,
  onGo,
  onClear,
}: {
  trail: TrailEntry[];
  onGo: (index: number) => void;
  onClear: () => void;
}) {
  const { t } = useTranslation();
  const [expanded, setExpanded] = useState(false);
  if (trail.length === 0) return null;
  const { hidden, shown } = visibleCrumbs(trail, PHONE_CRUMBS, expanded);
  return (
    <div className="flex flex-wrap items-center gap-2">
      <nav aria-label={t("quality.explorer.trail")}>
        <ol className="flex flex-wrap items-center gap-1 text-sm">
          {hidden > 0 ? (
            <li className="@2xl:hidden">
              <Button variant="ghost" size="sm" onClick={() => setExpanded(true)}>
                {t("quality.explorer.trailMore", { count: hidden })}
              </Button>
            </li>
          ) : null}
          {shown.map(({ item, index }, position) => {
            const last = index === trail.length - 1;
            return (
              <li key={`${index}-${item.side}-${item.q}`} className="flex items-center gap-1">
                {position > 0 || hidden > 0 ? <span aria-hidden="true">›</span> : null}
                {last ? (
                  <span aria-current="page" className="font-semibold">
                    {crumbLabel(item)}
                  </span>
                ) : (
                  <Button
                    variant="inline"
                    className="text-accent underline"
                    onClick={() => onGo(index)}
                  >
                    {crumbLabel(item)}
                  </Button>
                )}
              </li>
            );
          })}
        </ol>
      </nav>
      <Button variant="ghost" size="sm" onClick={onClear}>
        {t("quality.explorer.clearTrail")}
      </Button>
    </div>
  );
}

function Hits({
  facets,
  books,
  strongs,
  lemma,
  pos,
  openFilter,
}: {
  facets: QualityFacets;
  books: BibleStructureBook[];
  strongs?: string;
  lemma?: string;
  pos?: string;
  openFilter: (entry: TrailEntry) => void;
}) {
  const { t } = useTranslation();
  const [page, setPage] = useState(1);
  const query = useHits({ ...facets, strongs, lemma, pos, page, pageSize: HITS_PAGE_SIZE }, true);
  const jumps = useJumps(t, openFilter);
  return (
    <QueryState query={query}>
      {(data) => (
        <FindingsTable
          findings={data.items}
          total={data.total}
          page={data.page}
          pageSize={data.pageSize}
          onPage={setPage}
          columns={["ref", "german", "original"]}
          books={books}
          jumps={jumps}
          loading={query.isFetching}
        />
      )}
    </QueryState>
  );
}

function OriginalResult({
  detail,
  facets,
  books,
  openFilter,
}: {
  detail: OriginalDetailResponse;
  facets: QualityFacets;
  books: BibleStructureBook[];
  openFilter: (entry: TrailEntry) => void;
}) {
  const { t, i18n } = useTranslation();
  const num = (value: number) => formatNumber(value, i18n.language);
  const lang = detail.lang === "H" ? "he" : "grc";
  return (
    <div className="grid gap-4">
      <p className="flex flex-wrap items-baseline gap-x-3 gap-y-1">
        <span className="font-medium">{detail.strongs}</span>
        <span lang={lang} dir={lang === "he" ? "rtl" : undefined} className="font-original text-xl">
          {detail.lemma}
        </span>
        <span className="text-fg-muted">{detail.transliteration}</span>
        <span lang="en" className="text-fg-muted">
          {detail.gloss}
        </span>
        <span className="text-sm text-fg-muted">{detail.origClass}</span>
      </p>
      <p className="text-sm text-fg-muted">
        {t("quality.explorer.stats", {
          total: num(detail.occurrences),
          aligned: num(detail.aligned),
          unmapped: num(detail.unmapped),
        })}
      </p>
      <RankedBarList
        title={t("quality.explorer.renderings")}
        items={detail.renderings.map((r) => ({
          key: formatGermanQ(r.lemma, r.pos),
          label: `${r.lemma} (${r.pos})`,
          value: r.count,
          share: r.share,
          flags: flagsOf(t, r.flags),
        }))}
        onSelect={(key) => openFilter({ side: "d", q: key })}
        renderExpanded={(item) => {
          const filter = parseGermanQ(item.key);
          return (
            <Hits
              facets={facets}
              books={books}
              strongs={detail.strongs}
              lemma={filter.lemma}
              pos={filter.pos}
              openFilter={openFilter}
            />
          );
        }}
      />
    </div>
  );
}

function GermanResult({
  detail,
  facets,
  books,
  openFilter,
}: {
  detail: GermanDetailResponse;
  facets: QualityFacets;
  books: BibleStructureBook[];
  openFilter: (entry: TrailEntry) => void;
}) {
  const { t } = useTranslation();
  const isOriginal = (key: string) => normalizeOriginalQ(key) !== null;
  return (
    <div className="grid gap-4 @6xl:grid-cols-2">
      <div className="grid content-start gap-4">
        <p className="flex flex-wrap items-baseline gap-x-3">
          <span lang="de" className="font-scripture text-xl">
            {detail.lemma}
          </span>
          {detail.pos ? <span className="text-sm text-fg-muted">{detail.pos}</span> : null}
          <span className="text-sm text-fg-muted">
            {t("quality.explorer.germanStats", { count: detail.count })}
          </span>
        </p>
        <RankedBarList
          title={t("quality.explorer.sources")}
          items={detail.sources.map((s) => {
            const original = isOriginal(s.key);
            const lang = s.lang === "H" ? "he" : "grc";
            return {
              key: s.key,
              label: original ? (
                <>
                  <span
                    lang={lang}
                    dir={lang === "he" ? "rtl" : undefined}
                    className="font-original text-base"
                  >
                    {s.lemma}
                  </span>{" "}
                  <span>{s.key}</span>
                </>
              ) : s.key === "added" ? (
                t("quality.explorer.added")
              ) : (
                t("quality.explorer.none")
              ),
              sub: s.gloss,
              value: s.count,
              share: s.share,
              flags: flagsOf(t, s.flags),
              selectable: original,
            };
          })}
          onSelect={(key) => {
            const q = normalizeOriginalQ(key);
            if (q) openFilter({ side: "o", q });
          }}
          expandLabel={t("quality.explorer.verses")}
          renderExpanded={(item) => (
            <Hits
              facets={facets}
              books={books}
              strongs={item.key}
              lemma={detail.lemma}
              pos={detail.pos}
              openFilter={openFilter}
            />
          )}
        />
      </div>
      <section aria-label={t("quality.explorer.forms")} className="grid content-start gap-2">
        <h3 className="text-sm font-medium">{t("quality.explorer.forms")}</h3>
        <ul className="grid gap-2 text-sm">
          {detail.forms.map((form) => (
            <li key={form.form} className="flex flex-wrap items-baseline gap-x-2">
              <span lang="de" className="font-medium">
                {form.form}
              </span>
              <span className="tabular-nums text-fg-muted">{form.count}</span>
              {form.otherLemmas.length > 0 ? (
                <span className="flex flex-wrap items-baseline gap-1 text-fg-muted">
                  {t("quality.explorer.otherLemmas")}
                  {form.otherLemmas.map((other) => (
                    <Button
                      key={`${other.lemma}~${other.pos}`}
                      variant="inline"
                      className="text-accent underline"
                      onClick={() =>
                        openFilter({ side: "d", q: formatGermanQ(other.lemma, other.pos) })
                      }
                    >
                      {other.lemma} ({other.pos})
                    </Button>
                  ))}
                </span>
              ) : null}
            </li>
          ))}
        </ul>
      </section>
    </div>
  );
}

interface ExplorerProps {
  state: QualityUrlState;
  facets: QualityFacets;
  books: BibleStructureBook[];
  update: (patch: QualityPatch) => void;
  openFilter: (entry: TrailEntry) => void;
  goToCrumb: (index: number) => void;
}

export function Explorer({ state, facets, books, update, openFilter, goToCrumb }: ExplorerProps) {
  const { t } = useTranslation();
  const [text, setText] = useState("");
  const [pos, setPos] = useState("");
  const [debounced, setDebounced] = useState("");
  const heading = useRef<HTMLHeadingElement>(null);
  const mounted = useRef(false);
  const side: ExplorerSide = state.side;

  useEffect(() => {
    const timer = window.setTimeout(() => setDebounced(text.trim()), DEBOUNCE_MS);
    return () => window.clearTimeout(timer);
  }, [text]);

  // After a filter change (also via the trail) focus moves to the result heading.
  // biome-ignore lint/correctness/useExhaustiveDependencies: runs on filter changes only
  useEffect(() => {
    if (mounted.current && state.q) heading.current?.focus();
    mounted.current = true;
  }, [state.side, state.q]);

  const originalSearch = useOriginalSearch(debounced, side === "o");
  const germanSearch = useGermanSearch(debounced, pos, side === "d");

  const pick = (entry: TrailEntry) => {
    openFilter(entry);
    setText("");
    setDebounced("");
  };

  // One exact Strong's hit selects it directly.
  const originalData = originalSearch.data;
  // biome-ignore lint/correctness/useExhaustiveDependencies: `pick` is stable enough for this one-off
  useEffect(() => {
    if (side !== "o" || !originalData || originalData.q !== debounced) return;
    const only = originalData.candidates.length === 1 ? originalData.candidates[0] : undefined;
    const q = only?.matchedBy === "strongs" ? normalizeOriginalQ(only.strongs) : null;
    if (q) pick({ side: "o", q });
  }, [originalData, debounced, side]);

  const candidates =
    side === "o"
      ? (originalSearch.data?.candidates ?? []).map((c) => ({
          key: c.strongs,
          entry: { side: "o", q: normalizeOriginalQ(c.strongs) ?? c.strongs } as TrailEntry,
          content: (
            <>
              <span
                lang={c.lang === "H" ? "he" : "grc"}
                dir={c.lang === "H" ? "rtl" : undefined}
                className="font-original text-base"
              >
                {c.lemma}
              </span>{" "}
              <span>{c.transliteration}</span> <span className="text-fg-muted">{c.gloss}</span>
              <span className="text-fg-muted"> · {c.strongs}</span>
            </>
          ),
          matchedBy: c.matchedBy,
          count: c.occurrences,
        }))
      : (germanSearch.data?.candidates ?? []).map((c) => ({
          key: `${c.lemma}~${c.pos}`,
          entry: { side: "d", q: formatGermanQ(c.lemma, c.pos) } as TrailEntry,
          content: (
            <>
              <span lang="de">{c.lemma}</span> <span className="text-fg-muted">{c.pos}</span>
            </>
          ),
          matchedBy: c.matchedBy,
          count: c.count,
        }));

  const activeSearch = side === "o" ? originalSearch : germanSearch;
  const searching = debounced.length > 0;
  const submit = (event: FormEvent) => {
    event.preventDefault();
    const first = candidates[0];
    if (first) pick(first.entry);
  };

  const originalQ = side === "o" ? normalizeOriginalQ(state.q) : null;
  const german = side === "d" && state.q ? parseGermanQ(state.q) : null;
  const originalDetail = useOriginalDetail(originalQ ?? undefined, side === "o");
  const germanDetail = useGermanDetail(german?.lemma, german?.pos ?? "", side === "d");

  return (
    <section
      id={SECTION_IDS.explorer}
      aria-labelledby="quality-explorer-h"
      className="grid scroll-mt-20 gap-4"
    >
      <h2 id="quality-explorer-h" className="text-lg font-semibold">
        {t("quality.explorer.title")}
      </h2>
      <SectionHelp>
        <p>{t("quality.help.explorer.text")}</p>
      </SectionHelp>
      <fieldset className="flex gap-1">
        <legend className="sr-only">{t("quality.explorer.side")}</legend>
        {(["o", "d"] as const).map((value) => (
          <Button
            key={value}
            size="sm"
            aria-pressed={side === value}
            onClick={() => {
              setText("");
              update({ side: value, q: "" });
            }}
          >
            {t(`quality.explorer.${value === "o" ? "original" : "german"}`)}
          </Button>
        ))}
      </fieldset>
      <form onSubmit={submit} className="flex flex-wrap items-end gap-3">
        <div className="min-w-0 flex-1">
          <TextField
            label={t("quality.explorer.search")}
            value={text}
            onChange={setText}
            required={false}
            placeholder={t(
              side === "o"
                ? "quality.explorer.placeholderOriginal"
                : "quality.explorer.placeholderGerman",
            )}
          />
        </div>
        {side === "d" ? (
          <Select
            label={t("quality.explorer.pos")}
            value={pos}
            onChange={setPos}
            size="md"
            options={[
              { value: "", label: t("quality.explorer.anyPos") },
              ...POS_OPTIONS.map((value) => ({ value, label: value })),
            ]}
          />
        ) : null}
      </form>
      <p className="sr-only" aria-live="polite">
        {searching && activeSearch.data
          ? t("quality.explorer.occurrences", { count: candidates.length })
          : ""}
      </p>
      {searching ? (
        activeSearch.isError ? (
          <QueryState<unknown> query={activeSearch}>{() => null}</QueryState>
        ) : candidates.length > 0 ? (
          <ul aria-label={t("quality.explorer.pickCandidate")} className="grid gap-1">
            {candidates.map((c) => (
              <li key={c.key} className="flex flex-wrap items-center justify-between gap-2">
                <Button
                  variant="ghost"
                  size="sm"
                  className="justify-start"
                  onClick={() => pick(c.entry)}
                >
                  {c.content}
                </Button>
                <span className="flex items-center gap-2 text-xs text-fg-muted">
                  <span className="rounded-sm border border-border px-1">
                    {t(`quality.explorer.matchedBy.${c.matchedBy}`)}
                  </span>
                  <span className="tabular-nums">{c.count}</span>
                </span>
              </li>
            ))}
          </ul>
        ) : activeSearch.data ? (
          <EmptyState>
            <p>{t("quality.states.emptySearch", { q: debounced })}</p>
            <p>{t("quality.explorer.hint")}</p>
          </EmptyState>
        ) : null
      ) : null}
      <Trail trail={state.trail} onGo={goToCrumb} onClear={() => update(clearTrailPatch())} />
      {state.q ? (
        <div className="grid gap-3">
          <h3 ref={heading} tabIndex={-1} className="text-base font-medium">
            {side === "o" ? originalQ : german ? crumbLabel({ side: "d", q: state.q }) : state.q}
          </h3>
          {side === "o" ? (
            <QueryState query={originalDetail}>
              {(detail) => (
                <OriginalResult
                  detail={detail}
                  facets={facets}
                  books={books}
                  openFilter={openFilter}
                />
              )}
            </QueryState>
          ) : (
            <QueryState query={germanDetail}>
              {(detail) => (
                <GermanResult
                  detail={detail}
                  facets={facets}
                  books={books}
                  openFilter={openFilter}
                />
              )}
            </QueryState>
          )}
        </div>
      ) : (
        <p className="text-sm text-fg-muted">{t("quality.explorer.noSelection")}</p>
      )}
    </section>
  );
}
