import type { ReactNode } from "react";

export interface VerseSidePanelProps {
  /** `tsk`, `original`, `lxx` (roadmap 0028.4f) or `similar`; all stay in flow, so the verse row grows to the taller of German text and panel. */
  mode: "tsk" | "original" | "lxx" | "similar";
  /** accessible name, e.g. "Querverweise zu Vers 16" */
  label: string;
  children: ReactNode;
}

/**
 * Panel next to one verse in the left margin (from 120rem, placed by the `.verse-side` CSS). The verse row is a grid
 * with the panel in the same cell, so the panel lines up with its verse; the row is as tall as the taller of the two,
 * so every cross reference and the whole original text stay visible.
 */
export function VerseSidePanel({ mode, label, children }: VerseSidePanelProps) {
  return (
    // biome-ignore lint/a11y/useSemanticElements: a labelled group of links, not a form fieldset
    <div role="group" aria-label={label} data-mode={mode} className="verse-side font-sans">
      {children}
    </div>
  );
}
