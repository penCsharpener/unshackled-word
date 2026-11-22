import type { TFunction } from "i18next";

/** Qualifier segments that only mean something for some bases ("N" is neuter for nouns, but negative for particles). */
const NEGATIVE_BASES = new Set(["ADV", "PRT", "CONJ"]);
const DEGREE_BASES = new Set(["ADV", "A"]);

const NAME_SEGMENT = /^([PLT])(G?)$/;
const PRONOMINAL_SUFFIX = /^[A-Za-z]{2}[123][mfc]$/;

/** One STEP tag part such as `G:N-M`, `H:V`, `N:N-M-P` or `A:V`, spelled out in the UI language. */
function partLabel(part: string, t: TFunction): string | null {
  const text = part.trim();
  if (PRONOMINAL_SUFFIX.test(text)) return t("vocab.pos.pronominalSuffix");
  const match = /^(?:([GHAN]):)?(.+)$/.exec(text);
  if (!match) return null;
  const language = match[1];
  const [rawBase = "", ...segments] = (match[2] ?? "").split("-");
  const base = rawBase.toUpperCase();
  const isName = language === "N";
  const baseKey = isName ? "NAME" : base;
  if (!t(`vocab.pos.base.${baseKey}`, { defaultValue: "" })) return null;

  const details: string[] = [];
  for (const segment of segments.filter(Boolean)) {
    const upper = segment.toUpperCase();
    const name = isName ? NAME_SEGMENT.exec(upper) : null;
    if (name) {
      details.push(t(`vocab.pos.name.${name[1]}`));
      if (name[2]) details.push(t("vocab.pos.name.G"));
      continue;
    }
    const keys = upper.includes("/") ? upper.split("/") : [upper];
    const labels = keys.map((key) => {
      if (NEGATIVE_BASES.has(base) && key === "N") return t("vocab.pos.qualifier.negative");
      if (DEGREE_BASES.has(base) && key === "C") return t("vocab.pos.qualifier.comparative");
      if (DEGREE_BASES.has(base) && key === "S") return t("vocab.pos.qualifier.superlative");
      return t(`vocab.pos.qualifier.${key}`, { defaultValue: segment });
    });
    details.push(labels.join("/"));
  }
  if (language === "A") details.push(t("vocab.pos.aramaic"));
  const label = t(`vocab.pos.base.${baseKey}`);
  return details.length > 0 ? `${label} (${details.join(", ")})` : label;
}

/**
 * Spells out the part of speech of a vocab entry (STEP codes like `G:N-F` or `H:V`) in full, e.g.
 * "Nomen (feminin)". Several parts (`G:A / G:ADV`, `G:PRT-N+G:CONJ`) keep their separator; anything
 * the table does not know is shown unchanged rather than guessed.
 */
export function formatPos(pos: string | null | undefined, t: TFunction): string | null {
  if (!pos) return null;
  const pieces = pos.split(/(\s*[/+]\s*(?=[GHAN]:))/);
  const out = pieces.map((piece, index) => {
    if (index % 2 === 1) return piece.includes("+") ? " + " : " / ";
    return partLabel(piece, t) ?? piece.trim();
  });
  return out.join("");
}
