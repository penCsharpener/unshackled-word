/** Scrolls to an element by id; no animation under `prefers-reduced-motion`. */
export function scrollToId(id: string) {
  const element = document.getElementById(id);
  if (!element) return;
  const reduce =
    typeof window.matchMedia === "function" &&
    window.matchMedia("(prefers-reduced-motion: reduce)").matches;
  element.scrollIntoView({ behavior: reduce ? "auto" : "smooth", block: "start" });
}

export const SECTION_IDS = {
  coverage: "quality-coverage",
  explorer: "quality-explorer",
  findings: "quality-findings",
} as const;
