/** How a link in a rendered note is treated. */
export type LinkKind = "bible" | "external" | "unsafe";

const BIBLE_PATH = /^\/bible\/(\d{1,2})\/(\d{1,3})(?:\?v=([\d,-]+))?(?:#v\d+)?$/;

/** `/bible/…` (reader path made by the API) is routed in the SPA, http(s) opens in a new tab, everything else is not a link. */
export function classifyHref(href: string | null | undefined): LinkKind {
  if (!href) return "unsafe";
  if (BIBLE_PATH.test(href)) return "bible";
  if (/^https?:\/\/[^\s]+$/i.test(href)) return "external";
  return "unsafe";
}

/**
 * The `spans` parameter of `passageText` for a reader path (`/bible/45/12?v=1-2,5`), or undefined for a whole chapter
 * (no `v`): a card shows text for verses, not for chapters. Hebrew counting like the path itself.
 */
export function passageSpansOfPath(path: string): string | undefined {
  const match = BIBLE_PATH.exec(path);
  if (!match?.[3]) return undefined;
  const [, book, chapter, verses] = match;
  const base = Number(book) * 1_000_000 + Number(chapter) * 1_000;
  const spans = verses
    .split(",")
    .slice(0, 10)
    .map((range) => {
      const [from, to] = range.split("-").map(Number);
      return to === undefined || to === from
        ? String(base + (from ?? 0))
        : `${base + (from ?? 0)}-${base + to}`;
    });
  return spans.join(",");
}

const CODE = /(```[\s\S]*?```|~~~[\s\S]*?~~~|`[^`\n]*`)/;
const INLINE_LINK = /(!?)\[((?:[^[\]\\\n]|\\.)*)\]\(((?:[^()\\]|\\.|\([^()]*\))*)\)/g;
const AUTOLINK = /<([a-zA-Z][a-zA-Z0-9+.-]*:[^<>\s]*)>/g;

/**
 * Keeps only links that may be shown: `/bible/…` paths and http(s) URLs. A link with any other target (`javascript:`, `data:`,
 * relative paths …) is reduced to its text, an image to nothing; code stays as it is.
 */
export function dropUnsafeLinks(markdown: string): string {
  return markdown
    .split(CODE)
    .map((part, index) => {
      if (index % 2 === 1) return part;
      return part
        .replace(INLINE_LINK, (whole, bang: string, text: string, inner: string) => {
          const target = inner.trim().replace(/^<|>?\s.*$|>$/g, "");
          if (classifyHref(target) !== "unsafe") return whole;
          return bang ? "" : text;
        })
        .replace(AUTOLINK, (whole, target: string) =>
          classifyHref(target) === "external" ? whole : target,
        );
    })
    .join("");
}
