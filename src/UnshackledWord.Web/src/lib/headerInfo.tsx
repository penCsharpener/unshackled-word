import { createContext, type ReactNode, useContext, useState } from "react";
import type { DefinitionSegment, Morphology } from "./bible/types";

/** The original word shown in the middle of the sticky top menu while a word is hovered. */
export interface HeaderWordInfo {
  original: string;
  lemma?: string;
  strongs?: string;
  morphology?: Morphology;
  /** raw morphology code, used when no decoded morphology is available */
  morphologyCode?: string;
  language?: "greek" | "hebrew" | "aramaic";
  /** English Strong's gloss and (shortened) lexicon text, shown by the stacked hover card only */
  gloss?: string;
  definition?: string;
  definitionSegments?: DefinitionSegment[];
  /** Greek word not in all main editions (roadmap 0025.5f): "nur in TR, Byz" / "fehlt in WH, Treg" */
  editionNote?: string;
  /** Septuagint (roadmap 0028.5f), stacked card: status badge text, e.g. "Vorschlag" */
  badge?: string;
  /** Septuagint, stacked card: muted lines (Rahlfs reference, "Noch nicht bestimmt") */
  notes?: string[];
  /** shown in the page margin from 1024px (the reader), so the header leaves it out there */
  inMargin?: boolean;
}

const InfoContext = createContext<HeaderWordInfo | null>(null);
const SetInfoContext = createContext<(info: HeaderWordInfo | null) => void>(() => {});

export function HeaderInfoProvider({ children }: { children: ReactNode }) {
  const [info, setInfo] = useState<HeaderWordInfo | null>(null);
  return (
    <SetInfoContext.Provider value={setInfo}>
      <InfoContext.Provider value={info}>{children}</InfoContext.Provider>
    </SetInfoContext.Provider>
  );
}

export function useHeaderInfo(): HeaderWordInfo | null {
  return useContext(InfoContext);
}

export function useSetHeaderInfo(): (info: HeaderWordInfo | null) => void {
  return useContext(SetInfoContext);
}
