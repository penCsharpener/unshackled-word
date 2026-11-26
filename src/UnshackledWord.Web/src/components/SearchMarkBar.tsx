import { useTranslation } from "react-i18next";
import { Button } from "./Button";

export type SearchMarkStatus = "loading" | "hits" | "none" | "incomplete" | "error";

export interface SearchMarkBarProps {
  /** The marked query as typed. */
  query: string;
  status: SearchMarkStatus;
  /** `hits`: matched original words in the chapter. */
  count?: number;
  /** Reference of the previous / next chapter with a hit ("Matthäus 23"); absent = none, the arrow is disabled. */
  previous?: string;
  next?: string;
  onNavigate: (direction: "previous" | "next") => void;
  /** Opens the search overlay with the query. */
  onOpen: () => void;
  /** Turns marking off; the query stays. */
  onOff: () => void;
}

function Chevron({ direction }: { direction: "previous" | "next" }) {
  return (
    <svg
      aria-hidden="true"
      viewBox="0 0 24 24"
      className="size-4 rtl:-scale-x-100"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
    >
      <path d={direction === "previous" ? "m15 18-6-6 6-6" : "m9 18 6-6-6-6"} />
    </svg>
  );
}

/**
 * Reader chip of the search marks (roadmap 0029.2f): "Markiert: <query> · n Treffer", ‹ / › to the previous / next
 * chapter with a hit, × turns marking off. Clicking the text opens the search with the query.
 */
export function SearchMarkBar({
  query,
  status,
  count = 0,
  previous,
  next,
  onNavigate,
  onOpen,
  onOff,
}: SearchMarkBarProps) {
  const { t } = useTranslation();
  const incomplete = status === "incomplete";
  const result =
    status === "hits"
      ? t("search.mark.hits", { count })
      : status === "none"
        ? t("search.mark.none")
        : status === "error"
          ? t("search.mark.error")
          : status === "loading"
            ? t("search.mark.loading")
            : "";
  return (
    <div
      data-testid="search-mark-bar"
      className="inline-flex min-w-0 max-w-full flex-wrap items-center justify-center gap-1 rounded-md border border-border bg-surface ps-1 font-sans text-sm"
    >
      <span aria-hidden="true" className="ms-1 size-3 shrink-0 rounded-sm bg-mark-search" />
      <Button
        variant="ghost"
        size="sm"
        className="min-w-0"
        aria-haspopup="dialog"
        title={t("search.mark.open")}
        onClick={onOpen}
      >
        {incomplete ? (
          <span>{t("search.mark.incomplete")}</span>
        ) : (
          <span className="min-w-0 max-w-32 truncate sm:max-w-xs">
            <span className="text-fg-muted max-sm:sr-only">{t("search.mark.marked")} </span>
            <span className="font-semibold">{query}</span>
          </span>
        )}
      </Button>
      {!incomplete && (
        <span role="status" className="shrink-0 whitespace-nowrap text-fg-muted">
          {result}
        </span>
      )}
      {!incomplete && (
        <span className="flex shrink-0 items-center">
          <Button
            variant="ghost"
            size="sm"
            disabled={!previous}
            aria-label={
              previous
                ? t("search.mark.previous", { reference: previous })
                : t("search.mark.noPrevious")
            }
            title={previous}
            onClick={() => onNavigate("previous")}
          >
            <Chevron direction="previous" />
          </Button>
          <Button
            variant="ghost"
            size="sm"
            disabled={!next}
            aria-label={next ? t("search.mark.next", { reference: next }) : t("search.mark.noNext")}
            title={next}
            onClick={() => onNavigate("next")}
          >
            <Chevron direction="next" />
          </Button>
        </span>
      )}
      <Button
        variant="ghost"
        size="sm"
        className="shrink-0"
        aria-label={t("search.mark.off")}
        title={t("search.mark.off")}
        onClick={onOff}
      >
        <svg
          aria-hidden="true"
          viewBox="0 0 24 24"
          className="size-4"
          fill="none"
          stroke="currentColor"
          strokeWidth="2"
          strokeLinecap="round"
        >
          <path d="M18 6 6 18M6 6l12 12" />
        </svg>
      </Button>
    </div>
  );
}
