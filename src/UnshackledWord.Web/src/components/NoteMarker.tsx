import { Button } from "./Button";

export interface NoteMarkerProps {
  /** e.g. "2 Notizen zu Vers 28 öffnen" */
  label: string;
  /** notes on this anchor; a count is shown from two on */
  count?: number;
  /** at least one of them is the viewer's own: accent colour; others' notes use the `note-other` token */
  own?: boolean;
  onClick: () => void;
}

/** Small note icon at a verse number or the chapter heading; superscript, so it does not shift the line. */
export function NoteMarker({ label, count = 1, own = false, onClick }: NoteMarkerProps) {
  return (
    <Button
      variant="inline"
      aria-label={label}
      title={label}
      data-note-marker=""
      onClick={onClick}
      className={`inline-flex items-center gap-0.5 align-super font-sans text-xs leading-none hover:bg-surface-muted ${own ? "text-accent" : "text-note-other"}`}
    >
      <svg
        aria-hidden="true"
        viewBox="0 0 24 24"
        className="size-3.5"
        fill="none"
        stroke="currentColor"
        strokeWidth="2"
        strokeLinecap="round"
        strokeLinejoin="round"
      >
        <path d="M14.5 3H6a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8.5Z" />
        <path d="M14 3v6h6M8 13h8M8 17h5" />
      </svg>
      {count > 1 && <span aria-hidden="true">{count}</span>}
    </Button>
  );
}
