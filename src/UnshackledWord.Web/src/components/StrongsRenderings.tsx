import { useTranslation } from "react-i18next";
import type { StrongsRendering } from "@/lib/bible/types";
import { formatNumber } from "@/lib/chart";

/**
 * German renderings of a Strong's number as one line, most frequent first:
 * `sprechen (3.912) · sagen (1.204)`. A rendering key may hold several lemmas (`werden töten`); the
 * contraction row without an own lemma comes last, muted.
 */
export function StrongsRenderings({ renderings }: { renderings: StrongsRendering[] }) {
  const { t, i18n } = useTranslation();
  if (renderings.length === 0) return null;
  const named = renderings.filter((r) => !r.noOwnLemma).sort((a, b) => b.count - a.count);
  const own = renderings.find((r) => r.noOwnLemma);
  const count = (n: number) => formatNumber(n, i18n.language);
  return (
    <p className="text-sm @3xl:col-span-full" data-testid="strongs-renderings">
      {named.map((r, index) => (
        <span key={`${r.key}-${r.pos ?? ""}`}>
          {index > 0 ? " · " : null}
          <span lang="de">{r.word}</span>
          <span className="text-fg-muted"> ({count(r.count)})</span>
        </span>
      ))}
      {own ? (
        <span className="text-fg-muted">
          {named.length > 0 ? " · " : null}
          {t("bible.strongs.renderings.noOwnLemma", { count: count(own.count) })}
        </span>
      ) : null}
    </p>
  );
}
