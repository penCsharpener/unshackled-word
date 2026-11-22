import type { ReactNode } from "react";

export interface StudyCardProps {
  /** Kind of the card in capitals, e.g. "Neues Wort". */
  kindLabel: string;
  /** Prompt and answer area of the card, including its feedback. */
  children: ReactNode;
}

/** Frame of one study card (roadmap 0022.8f): the kind label above everything the card shows. */
export function StudyCard({ kindLabel, children }: StudyCardProps) {
  return (
    <article className="grid gap-5 rounded-lg border border-border bg-surface p-4 md:p-6">
      <p className="text-xs font-medium uppercase tracking-wide text-fg-muted">{kindLabel}</p>
      {children}
    </article>
  );
}
