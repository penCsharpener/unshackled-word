import type { ReactNode } from "react";

export interface SuggestionItem {
  /** Stable key of the entry. */
  id: string;
  label: ReactNode;
  /** Muted second part after the label (gloss, language note). */
  detail?: ReactNode;
  /** Right-aligned extra, e.g. a count. */
  trailing?: ReactNode;
  /** Shown muted, e.g. a value without occurrences. */
  dimmed?: boolean;
}

export interface SuggestionListProps {
  id: string;
  label: string;
  items: SuggestionItem[];
  /** Highlighted entry (`aria-activedescendant` of the field), -1 for none. */
  activeIndex: number;
  onAccept: (index: number) => void;
}

/** Option id for `aria-activedescendant`. */
export const suggestionOptionId = (listId: string, index: number) =>
  `${listId}-suggestion-${index}`;

/**
 * Listbox of autocomplete suggestions under a field (`TextField` `suggestions`): the field keeps the focus, the
 * keyboard is handled there; a click accepts an entry.
 */
export function SuggestionList({ id, label, items, activeIndex, onAccept }: SuggestionListProps) {
  return (
    <div
      id={id}
      role="listbox"
      aria-label={label}
      className="absolute inset-x-0 top-full z-40 mt-1 grid max-h-80 overflow-y-auto rounded-md border border-border bg-surface py-1 text-sm text-fg shadow-sm"
    >
      {items.map((item, index) => (
        // biome-ignore lint/a11y/useKeyWithClickEvents: the keyboard stays in the combobox field (aria-activedescendant)
        <div
          key={item.id}
          id={suggestionOptionId(id, index)}
          role="option"
          tabIndex={-1}
          aria-selected={index === activeIndex}
          data-active={index === activeIndex || undefined}
          // Keeps the focus in the field.
          onMouseDown={(event) => event.preventDefault()}
          onClick={() => onAccept(index)}
          className={`flex cursor-pointer items-baseline gap-2 px-3 py-1.5 hover:bg-surface-muted data-active:bg-surface-muted data-active:outline-2 data-active:-outline-offset-2 data-active:outline-focus ${
            item.dimmed ? "text-fg-muted" : ""
          }`}
        >
          <span className="min-w-0 truncate">{item.label}</span>
          {item.detail ? (
            <span className="min-w-0 truncate text-xs text-fg-muted">{item.detail}</span>
          ) : null}
          {item.trailing !== undefined ? (
            <span className="ms-auto shrink-0 text-xs text-fg-muted tabular-nums">
              {item.trailing}
            </span>
          ) : null}
        </div>
      ))}
    </div>
  );
}
