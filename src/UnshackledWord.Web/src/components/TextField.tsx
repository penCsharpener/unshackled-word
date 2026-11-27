import {
  type InputHTMLAttributes,
  type KeyboardEvent,
  type ReactNode,
  type Ref,
  type TextareaHTMLAttributes,
  useId,
} from "react";
import { SEARCH_ICON_PATHS } from "@/lib/search/icon";
import { type SuggestionItem, SuggestionList, suggestionOptionId } from "./SuggestionList";

export interface TextFieldProps
  extends Omit<InputHTMLAttributes<HTMLInputElement>, "onChange" | "value" | "size"> {
  label: string;
  value: string;
  onChange: (value: string) => void;
  size?: "sm" | "md";
  /** Keeps the label for assistive tech only, e.g. for an editor inside a table cell. */
  labelHidden?: boolean;
  /** Error style (accent border) */
  invalid?: boolean;
  inputRef?: Ref<HTMLInputElement>;
  /** Leading icon inside the field; `type="search"` gets a magnifier by default. */
  icon?: ReactNode;
  /** Renders a `<textarea>` (no leading icon) instead of a single-line input. */
  multiline?: boolean;
  /** Visible lines of a `multiline` field. */
  rows?: number;
  /**
   * Autocomplete entries shown in a `SuggestionList` under the field (ARIA combobox while the list is open).
   * While the list is open the field handles ↑/↓ (`onActiveSuggestionChange`), Tab (accepts the active or first entry),
   * Enter (accepts the active entry; without one it falls through to `onKeyDown`) and Esc (`onSuggestionsDismiss`,
   * not passed on, so a surrounding dialog stays open). Other keys and a closed list go to `onKeyDown`.
   */
  suggestions?: SuggestionItem[];
  /** Highlighted entry, -1 (default) for none. */
  activeSuggestion?: number;
  onSuggestionAccept?: (index: number) => void;
  onActiveSuggestionChange?: (index: number) => void;
  onSuggestionsDismiss?: () => void;
  /** Accessible name of the suggestion list. */
  suggestionsLabel?: string;
}

const sizes = {
  sm: "px-2 py-1 text-sm",
  md: "px-3 py-2 text-base",
} as const;

function SearchIcon() {
  return (
    <svg
      aria-hidden="true"
      viewBox="0 0 24 24"
      className="size-5"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
    >
      {SEARCH_ICON_PATHS.map((d) => (
        <path key={d} d={d} />
      ))}
    </svg>
  );
}

/** Labelled text input (also email / password / search) styled with tokens; required by default. */
export function TextField({
  label,
  value,
  onChange,
  type = "text",
  required = true,
  size = "md",
  labelHidden = false,
  invalid = false,
  inputRef,
  icon,
  multiline = false,
  rows = 4,
  className = "",
  suggestions,
  activeSuggestion = -1,
  onSuggestionAccept,
  onActiveSuggestionChange,
  onSuggestionsDismiss,
  suggestionsLabel,
  onKeyDown,
  ...props
}: TextFieldProps) {
  const id = useId();
  const listId = `${id}-suggestions`;
  if (multiline) {
    return (
      <div className="flex flex-col gap-1 text-sm">
        <label htmlFor={id} className={labelHidden ? "sr-only" : undefined}>
          {label}
        </label>
        <textarea
          id={id}
          rows={rows}
          value={value}
          required={required}
          aria-invalid={invalid || undefined}
          onChange={(event) => onChange(event.target.value)}
          className={`rounded-md border bg-surface text-fg ${invalid ? "border-accent" : "border-border"} ${sizes[size]} ${className}`}
          {...(props as TextareaHTMLAttributes<HTMLTextAreaElement>)}
          onKeyDown={onKeyDown as TextareaHTMLAttributes<HTMLTextAreaElement>["onKeyDown"]}
        />
      </div>
    );
  }
  const leading = icon ?? (type === "search" ? <SearchIcon /> : null);
  const items = suggestions ?? [];
  const listOpen = items.length > 0;
  const active = activeSuggestion >= 0 && activeSuggestion < items.length ? activeSuggestion : -1;

  const handleKeyDown = (event: KeyboardEvent<HTMLInputElement>) => {
    if (listOpen && !event.altKey && !event.ctrlKey && !event.metaKey) {
      const move = event.key === "ArrowDown" ? 1 : event.key === "ArrowUp" ? -1 : 0;
      if (move !== 0) {
        event.preventDefault();
        const next = active < 0 && move < 0 ? items.length - 1 : active + move;
        onActiveSuggestionChange?.((next + items.length) % items.length);
        return;
      }
      if (event.key === "Tab" && !event.shiftKey) {
        event.preventDefault();
        onSuggestionAccept?.(active >= 0 ? active : 0);
        return;
      }
      if (event.key === "Enter" && active >= 0) {
        event.preventDefault();
        onSuggestionAccept?.(active);
        return;
      }
      if (event.key === "Escape") {
        // Closes the list only: the event does not reach a surrounding dialog's Esc handler.
        event.preventDefault();
        event.stopPropagation();
        onSuggestionsDismiss?.();
        return;
      }
    }
    onKeyDown?.(event);
  };

  const combobox = listOpen
    ? {
        role: "combobox",
        "aria-autocomplete": "list" as const,
        "aria-expanded": true,
        "aria-controls": listId,
        "aria-activedescendant": active >= 0 ? suggestionOptionId(listId, active) : undefined,
      }
    : {};
  const input = (
    <input
      id={id}
      ref={inputRef}
      type={type}
      value={value}
      required={required}
      aria-invalid={invalid || undefined}
      onChange={(event) => onChange(event.target.value)}
      className={`rounded-md border bg-surface text-fg ${invalid ? "border-accent" : "border-border"} ${sizes[size]} ${leading ? "w-full ps-10" : ""} ${className}`}
      {...props}
      {...combobox}
      onKeyDown={handleKeyDown}
    />
  );
  const list = listOpen ? (
    <SuggestionList
      id={listId}
      label={suggestionsLabel ?? label}
      items={items}
      activeIndex={active}
      onAccept={(index) => onSuggestionAccept?.(index)}
    />
  ) : null;
  return (
    <div className="flex flex-col gap-1 text-sm">
      <label htmlFor={id} className={labelHidden ? "sr-only" : undefined}>
        {label}
      </label>
      {leading || suggestions ? (
        <div className="relative">
          {leading ? (
            <span
              aria-hidden="true"
              className="pointer-events-none absolute inset-y-0 start-3 flex items-center text-fg-muted"
            >
              {leading}
            </span>
          ) : null}
          {input}
          {list}
        </div>
      ) : (
        input
      )}
    </div>
  );
}
