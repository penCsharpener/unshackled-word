import { type KeyboardEvent, type ReactNode, useRef } from "react";
import { Button } from "@/components/Button";

export interface TabItem {
  key: string;
  label: string;
}

export interface TabsProps {
  /** Accessible name of the tab list. */
  label: string;
  tabs: TabItem[];
  active: string;
  onChange: (key: string) => void;
  /** Prefix of the tab and panel ids (unique per page). */
  idPrefix?: string;
  /** Content of the active tab only: the caller renders just that one, so inactive tabs fetch nothing. */
  children: ReactNode;
}

/**
 * Tab list (`role="tablist"`, roving tab stop, arrow keys / Home / End switch tabs) with one panel
 * for the active tab. The list scrolls horizontally when it does not fit.
 */
export function Tabs({ label, tabs, active, onChange, idPrefix = "tabs", children }: TabsProps) {
  const refs = useRef(new Map<string, HTMLButtonElement>());

  function onKeyDown(event: KeyboardEvent<HTMLDivElement>) {
    const index = tabs.findIndex((tab) => tab.key === active);
    const last = tabs.length - 1;
    const next =
      event.key === "ArrowRight"
        ? index >= last
          ? 0
          : index + 1
        : event.key === "ArrowLeft"
          ? index <= 0
            ? last
            : index - 1
          : event.key === "Home"
            ? 0
            : event.key === "End"
              ? last
              : -1;
    const key = tabs[next]?.key;
    if (key === undefined) return;
    event.preventDefault();
    onChange(key);
    refs.current.get(key)?.focus();
  }

  return (
    <div className="flex min-w-0 flex-col gap-4">
      <div
        role="tablist"
        aria-label={label}
        onKeyDown={onKeyDown}
        className="flex min-w-0 gap-1 overflow-x-auto border-b border-border pb-1"
      >
        {tabs.map((tab) => {
          const selected = tab.key === active;
          return (
            <Button
              key={tab.key}
              ref={(element) => {
                if (element) refs.current.set(tab.key, element);
                else refs.current.delete(tab.key);
              }}
              id={`${idPrefix}-tab-${tab.key}`}
              role="tab"
              variant="ghost"
              size="sm"
              aria-selected={selected}
              aria-controls={`${idPrefix}-panel-${tab.key}`}
              tabIndex={selected ? 0 : -1}
              onClick={() => onChange(tab.key)}
              className="shrink-0 whitespace-nowrap aria-selected:bg-surface-muted aria-selected:font-semibold aria-selected:text-accent"
            >
              {tab.label}
            </Button>
          );
        })}
      </div>
      <div
        role="tabpanel"
        id={`${idPrefix}-panel-${active}`}
        aria-labelledby={`${idPrefix}-tab-${active}`}
        className="flex min-w-0 flex-col gap-6"
      >
        {children}
      </div>
    </div>
  );
}
