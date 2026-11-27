import type { CheckSummary, Finding } from "./types";
import { normalizeOriginalQ, type TrailEntry } from "./url";

export type TileSeverity = "error" | "warning" | "info" | "ok";
export type CheckGroupKey = "mapping" | "lemma" | "roles" | "tsk" | "editions" | "vocab";

/** No findings = ok, any error finding = error, otherwise worth a look. */
export function tileSeverity(check: Pick<CheckSummary, "count" | "errorCount">): TileSeverity {
  if (check.count === 0) return "ok";
  return check.errorCount > 0 ? "error" : "warning";
}

/** L* checks are about the German lemma / morphology, R* about link roles, T* about TSK curations, G* about Greek editions, V* about vocab roots, everything else about the mapping. */
export function checkGroup(code: string): CheckGroupKey {
  if (code.startsWith("R")) return "roles";
  if (code.startsWith("T")) return "tsk";
  if (code.startsWith("G")) return "editions";
  if (code.startsWith("V")) return "vocab";
  return code.startsWith("L") ? "lemma" : "mapping";
}

/** The shown tiles: tier 1 and 2 (tier 3 is not surfaced), in code order within each group. */
export function visibleChecks(checks: readonly CheckSummary[]): CheckSummary[] {
  return checks
    .filter((check) => check.tier <= 2)
    .sort((a, b) => a.code.localeCompare(b.code, "en", { numeric: true }));
}

export function deltaOf(check: CheckSummary): number | undefined {
  return check.previousCount === undefined ? undefined : check.count - check.previousCount;
}

/** Check that lists the unmapped words of an `unmapped/{side}` panel. */
export const UNMAPPED_CHECK = { german: "U1", greek: "U2", hebrew: "U3" } as const;

/** Explorer filter (side + q) for a ranked row key of the unmapped panels or a finding. */
export function filterForKey(key: string): TrailEntry | null {
  const original = normalizeOriginalQ(key);
  if (original) return { side: "o", q: original };
  return key ? { side: "d", q: key } : null;
}

/** Checks that list groups first; findings only come back with `group=<key>` (M4 lists ot/nt next to its items). */
export const AGGREGATED_CHECKS: readonly string[] = ["C1", "M1", "M2", "L2", "L3", "L4"];

/** Aggregated checks whose group expands into findings (C1 chapters are a list only). */
export const EXPANDABLE_CHECKS: readonly string[] = ["M1", "M2", "L2", "L3", "L4"];

/**
 * `context` is about five words either side of the German word. Splits it at the occurrence of `word`
 * closest to the middle; without a usable word the whole text goes left.
 */
export function splitContext(
  context: string | undefined,
  word: string | undefined,
): { left: string; right: string } | null {
  if (!context) return null;
  if (!word) return { left: context, right: "" };
  const middle = context.length / 2;
  let best = -1;
  for (let at = context.indexOf(word); at >= 0; at = context.indexOf(word, at + 1)) {
    if (
      best < 0 ||
      Math.abs(at + word.length / 2 - middle) < Math.abs(best + word.length / 2 - middle)
    )
      best = at;
  }
  if (best < 0) return { left: context, right: "" };
  return { left: context.slice(0, best), right: context.slice(best + word.length) };
}

// ---- jump targets ---------------------------------------------------------------------------

export type JumpKind = "mapping" | "lemma" | "bulk" | "strongs" | "verse" | "target" | "ref";

export interface JumpTarget {
  kind: JumpKind;
  href: string;
}

const enc = encodeURIComponent;

/** Router paths of the fix places; all open in a new tab. Only the targets a finding has data for. */
export function jumpTargets(finding: Finding): JumpTarget[] {
  const { bibleBookId: book, chapter, verse } = finding;
  const targets: JumpTarget[] = [];
  if (finding.targetBibleBookId !== undefined && finding.targetChapter !== undefined) {
    // TSK curation findings: open the source verse and the target verse of the link
    targets.push({ kind: "verse", href: `/bible/${book}/${chapter}#v${verse}` });
    targets.push({
      kind: "target",
      href: `/bible/${finding.targetBibleBookId}/${finding.targetChapter}#v${finding.targetVerse}`,
    });
  } else if (finding.check.startsWith("G") && book > 0) {
    // Greek edition findings: open the verse of the word (G1: the first word with the unknown code)
    targets.push({ kind: "ref", href: `/bible/${book}/${chapter}#v${verse}` });
  }
  if (finding.elbWordId !== undefined) {
    const stepPart = finding.stepWordId !== undefined ? `&s=${finding.stepWordId}` : "";
    targets.push({
      kind: "mapping",
      href: `/bible/${book}/${chapter}?map=${verse}&w=${finding.elbWordId}${stepPart}#v${verse}`,
    });
    targets.push({
      kind: "lemma",
      href: `/edit/lemmas/${book}/${chapter}?word=${finding.elbWordId}`,
    });
  }
  if (finding.lemma) {
    const orig = finding.strongs ? `&orig=${enc(finding.strongs)}` : "";
    targets.push({
      kind: "bulk",
      href: `/edit/lemmas/${book}/${chapter}?dlg=search&from=${enc(finding.lemma)}${orig}`,
    });
  }
  if (finding.strongs) {
    const rendering = finding.lemma ? `?rendering=${enc(finding.lemma)}` : "";
    targets.push({ kind: "strongs", href: `/bible/strongs/${enc(finding.strongs)}${rendering}` });
  }
  return targets;
}

/** J-F: the explorer filter a finding leads to (original side when it has a Strong's number). */
export function filterForFinding(finding: Finding): TrailEntry | null {
  if (finding.strongs) {
    const q = normalizeOriginalQ(finding.strongs);
    if (q) return { side: "o", q };
  }
  if (finding.lemma)
    return { side: "d", q: finding.pos ? `${finding.lemma}~${finding.pos}` : finding.lemma };
  return null;
}

// ---- heatmap --------------------------------------------------------------------------------

/** Lower bounds (share of unaligned words) of heat levels 1..5: any gap, 2 %, 5 %, 10 %, 20 %. */
export const SHARE_SCALE = [Number.EPSILON, 0.02, 0.05, 0.1, 0.2] as const;

/** Heat level 0..5 = number of scale bounds the value reaches. */
export function heatLevel(value: number, scale: readonly number[]): number {
  return scale.reduce((level, bound) => (value >= bound ? level + 1 : level), 0);
}
