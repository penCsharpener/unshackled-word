import { useState } from "react";
import { useTranslation } from "react-i18next";
import { BookBarChart } from "@/components/BookBarChart";
import { Button } from "@/components/Button";
import { DonutChart } from "@/components/DonutChart";
import { RankedBarList } from "@/components/RankedBarList";
import type { BibleStructureBook } from "@/lib/bible/types";
import { UNMAPPED_CHECK } from "@/lib/quality/checks";
import { useUnmapped } from "@/lib/quality/queries";
import { SECTION_IDS, scrollToId } from "@/lib/quality/scroll";
import type { QualityFacets, UnmappedSide, UnmappedTop } from "@/lib/quality/types";
import type { QualityPatch, TrailEntry } from "@/lib/quality/url";
import { QueryState } from "./QueryState";

const SIDES: UnmappedSide[] = ["german", "greek", "hebrew"];

interface Props {
  facets: QualityFacets;
  books: BibleStructureBook[];
  functionWords: boolean;
  check?: string;
  classFacet?: string;
  selectedBookId?: number;
  update: (patch: QualityPatch) => void;
  openFilter: (entry: TrailEntry) => void;
}

function topLabel(side: UnmappedSide, item: UnmappedTop) {
  if (side === "german") return item.label;
  const lang = side === "hebrew" ? "he" : "grc";
  return (
    <>
      {item.original ? (
        <span
          lang={lang}
          dir={side === "hebrew" ? "rtl" : undefined}
          className="font-original text-base"
        >
          {item.original}
        </span>
      ) : null}{" "}
      <span>{item.label}</span>
    </>
  );
}

function UnmappedCard({ side, ...props }: Props & { side: UnmappedSide }) {
  const { t } = useTranslation();
  const [byBook, setByBook] = useState(false);
  const code = UNMAPPED_CHECK[side];
  const query = useUnmapped(side, props.facets, !props.functionWords, true);
  const active = props.check === code;

  return (
    <article className="grid content-start gap-4 rounded-lg border border-border bg-surface p-4">
      <QueryState query={query}>
        {(data) => (
          <>
            <DonutChart
              title={t(`quality.checks.${code}.title`)}
              subtitle={t("quality.unmapped.total", { count: data.total })}
              slices={data.byClass.map((entry) => ({
                key: entry.key,
                label: entry.legit
                  ? `${entry.label} (${t("quality.unmapped.legit")})`
                  : entry.label,
                text: entry.label,
                value: entry.count,
              }))}
              centerValue={data.total}
              activeKey={active ? (props.classFacet ?? null) : null}
              onSelectSlice={(key) => {
                props.update({ check: code, facets: key ? { class: key } : {} });
                if (key) scrollToId(SECTION_IDS.findings);
              }}
            />
            <div className="grid gap-2">
              <Button
                variant="ghost"
                size="sm"
                className="justify-self-end"
                aria-pressed={byBook}
                onClick={() => setByBook((value) => !value)}
              >
                {t("quality.unmapped.byBook")}
              </Button>
              {byBook ? (
                <BookBarChart
                  books={props.books}
                  counts={data.byBook}
                  testaments={props.facets.testament ? [props.facets.testament] : ["ot", "nt"]}
                  title={t("quality.unmapped.byBook")}
                  selectedBookId={props.selectedBookId ?? null}
                  onSelectBook={(bookId) => props.update({ bookId: bookId ?? undefined })}
                  defaultView="list"
                />
              ) : (
                <RankedBarList
                  title={t("quality.unmapped.top")}
                  items={data.top.map((item) => ({
                    key: item.key,
                    label: topLabel(side, item),
                    sub: item.gloss ?? item.pos,
                    value: item.count,
                  }))}
                  onSelect={(key) => {
                    props.openFilter({ side: side === "german" ? "d" : "o", q: key });
                    scrollToId(SECTION_IDS.explorer);
                  }}
                />
              )}
            </div>
          </>
        )}
      </QueryState>
    </article>
  );
}

export function UnmappedSection(props: Props) {
  const { t } = useTranslation();
  return (
    <section aria-labelledby="quality-unmapped" className="grid gap-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h2 id="quality-unmapped" className="text-lg font-semibold">
          {t("quality.sections.unmapped")}
        </h2>
        <Button
          variant="secondary"
          size="sm"
          aria-pressed={props.functionWords}
          onClick={() => props.update({ functionWords: !props.functionWords })}
        >
          {t("quality.unmapped.withFunction")}
        </Button>
      </div>
      <div className="grid gap-4 @6xl:grid-cols-3">
        {SIDES.map((side) => (
          <UnmappedCard key={side} side={side} {...props} />
        ))}
      </div>
    </section>
  );
}
