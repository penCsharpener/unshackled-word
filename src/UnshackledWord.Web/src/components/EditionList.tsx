import { useId, useState } from "react";
import { useTranslation } from "react-i18next";
import type { EditionRef, GreekEdition, ReadingEditions } from "@/lib/bible/types";
import { Button } from "./Button";
import { StatusBadge } from "./StatusBadge";

export interface EditionListProps {
  editions: ReadingEditions;
  /** catalog from `useGreekEditions`; without it the chips show the codes only */
  catalog?: GreekEdition[];
}

type ChipState = "present" | "missing" | "other";

/** `«14.24` (placed at 14,24 in that edition) → chapter and verse; other suffixes stay raw. */
const CROSS_VERSE = /^[«»](\d+)\.(\d+)$/;

/** Full name of an edition: the German catalog name, English names from i18n by code (catalog name as fallback). */
function useEditionName(catalog: GreekEdition[] | undefined) {
  const { t, i18n } = useTranslation();
  const english = i18n.language.startsWith("en");
  return (code: string): { name?: string; year?: number } => {
    const row = catalog?.find((e) => e.code === code);
    const key = `bible.wordDetail.editions.name.${code}`;
    const translated = i18n.exists(key) ? t(key) : undefined;
    const name = english ? (translated ?? row?.name) : (row?.name ?? translated);
    return { name, year: row?.year };
  };
}

/** Which Greek text editions have this word (roadmap 0025): present main editions, "fehlt in" and non-main tokens. */
export function EditionList({ editions, catalog }: EditionListProps) {
  const { t } = useTranslation();
  const headingId = useId();
  const detailId = useId();
  const nameOf = useEditionName(catalog);
  const [active, setActive] = useState<{ key: string; ref: EditionRef; pinned: boolean } | null>(
    null,
  );

  const presentMain = editions.present.filter((e) => e.kind === "edition");
  const others = editions.present.filter((e) => e.kind !== "edition");
  const only = editions.missing.length > 0 && presentMain.length < editions.missing.length;

  const shiftText = (ref: EditionRef): string | undefined => {
    if (ref.shift !== undefined && ref.shift !== 0)
      return t(`bible.wordDetail.editions.${ref.shift > 0 ? "later" : "earlier"}`, {
        count: Math.abs(ref.shift),
      });
    if (!ref.shiftRaw) return undefined;
    const place = CROSS_VERSE.exec(ref.shiftRaw);
    return place
      ? t("bible.wordDetail.editions.otherPlace", { chapter: place[1], verse: place[2] })
      : t("bible.wordDetail.editions.otherOrder", { raw: ref.shiftRaw });
  };

  const describe = (ref: EditionRef): string => {
    const { name, year } = nameOf(ref.code);
    const parts = [name ?? t("bible.wordDetail.editions.notInCatalog")];
    if (year !== undefined) parts.push(String(year));
    if (ref.kind !== "edition") parts.push(t(`bible.wordDetail.editions.kind.${ref.kind}`));
    return `${ref.code}: ${parts.join(", ")}`;
  };

  // A plain render function, not a nested component: a component defined here would remount on every state change and drop the focus.
  const chip = (edition: EditionRef, state: ChipState) => {
    const key = `${state === "missing" ? "m" : "p"}-${edition.code}`;
    const shift = state === "missing" ? undefined : shiftText(edition);
    const isActive = active?.key === key;
    return (
      <li key={key}>
        <Button
          variant="inline"
          aria-describedby={isActive ? detailId : undefined}
          aria-pressed={isActive && active?.pinned ? true : undefined}
          onMouseEnter={() =>
            setActive((a) => (a?.pinned ? a : { key, ref: edition, pinned: false }))
          }
          onMouseLeave={() => setActive((a) => (a?.pinned ? a : null))}
          onFocus={() =>
            setActive((a) => (a?.key === key ? a : { key, ref: edition, pinned: false }))
          }
          onBlur={() => setActive((a) => (a?.key === key ? null : a))}
          onClick={() =>
            setActive((a) =>
              a?.key === key && a.pinned ? null : { key, ref: edition, pinned: true },
            )
          }
        >
          <StatusBadge tone="neutral">
            <span className={state === "present" ? "" : "text-fg-muted"}>
              {state === "missing" ? (
                <>
                  <s>{edition.code}</s>
                  <span className="sr-only"> {t("bible.wordDetail.editions.missingSr")}</span>
                </>
              ) : (
                edition.code
              )}
              {state === "other" && <> · {t(`bible.wordDetail.editions.kind.${edition.kind}`)}</>}
              {shift && <span className="font-normal text-fg-muted"> {shift}</span>}
            </span>
          </StatusBadge>
        </Button>
      </li>
    );
  };

  return (
    <section aria-labelledby={headingId} className="mt-2 text-sm">
      <details>
        <summary className="cursor-pointer text-fg-muted">
          <h4 id={headingId} className="inline">
            {t("bible.wordDetail.editions.title")}
          </h4>{" "}
          <span aria-hidden="true">ⓘ</span>
          <span className="sr-only"> {t("bible.wordDetail.editions.infoSr")}</span>
        </summary>
        <p className="mt-1 text-xs text-fg-muted">{t("bible.wordDetail.editions.info")}</p>
      </details>
      <div className="mt-1 flex flex-wrap items-center gap-x-3 gap-y-1">
        {presentMain.length > 0 && (
          <div className="flex flex-wrap items-center gap-1">
            {only && <span className="text-fg-muted">{t("bible.wordDetail.editions.only")}</span>}
            <ul
              aria-label={t("bible.wordDetail.editions.presentLabel")}
              className="flex flex-wrap gap-1"
            >
              {presentMain.map((e) => chip(e, "present"))}
            </ul>
          </div>
        )}
        {editions.missing.length > 0 && (
          <div className="flex flex-wrap items-center gap-1">
            <span className="text-fg-muted">{t("bible.wordDetail.editions.missing")}</span>
            <ul
              aria-label={t("bible.wordDetail.editions.missingLabel")}
              className="flex flex-wrap gap-1"
            >
              {editions.missing.map((e) => chip(e, "missing"))}
            </ul>
          </div>
        )}
      </div>
      {others.length > 0 && (
        <ul
          aria-label={t("bible.wordDetail.editions.otherLabel")}
          className="mt-1 flex flex-wrap gap-1"
        >
          {others.map((e) => chip(e, "other"))}
        </ul>
      )}
      {active && (
        <p id={detailId} className="mt-1 text-xs text-fg-muted">
          {describe(active.ref)}
        </p>
      )}
    </section>
  );
}
