import { createContext, type ReactNode, useContext, useMemo, useState } from "react";
import type { Morphology } from "./bible/types";

/** The original word shown in the middle of the sticky top menu while a word is hovered. */
export interface HeaderWordInfo {
  original: string;
  lemma?: string;
  strongs?: string;
  morphology?: Morphology;
  /** raw morphology code, used when no decoded morphology is available */
  morphologyCode?: string;
  language?: "greek" | "hebrew" | "aramaic";
}

const InfoContext = createContext<HeaderWordInfo | null>(null);
const SetInfoContext = createContext<(info: HeaderWordInfo | null) => void>(() => {});

export function HeaderInfoProvider({ children }: { children: ReactNode }) {
  const [info, setInfo] = useState<HeaderWordInfo | null>(null);
  const setter = useMemo(() => setInfo, []);
  return (
    <SetInfoContext.Provider value={setter}>
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
