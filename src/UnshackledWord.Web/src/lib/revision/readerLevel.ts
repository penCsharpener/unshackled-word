import { useCallback, useState } from "react";
import { BASE_LEVEL } from "@/lib/bible/api";
import { useRevisionLevels } from "./queries";
import type { RevisionLevel } from "./types";

export const READER_LEVEL_KEY = "uw.readerLevel";

function readStoredLevel(): number | undefined {
  try {
    const raw = localStorage.getItem(READER_LEVEL_KEY);
    if (raw === null || !/^\d+$/.test(raw)) return undefined;
    return Number(raw);
  } catch {
    return undefined;
  }
}

function writeStoredLevel(level: number): void {
  try {
    localStorage.setItem(READER_LEVEL_KEY, String(level));
  } catch {
    // storage unavailable: the choice only lasts for this page view
  }
}

/** One slider stop per level; where Elb1871 shares its level with a revision, the revision names the stop (it wins there). */
export function sliderStops(levels: RevisionLevel[]): RevisionLevel[] {
  const stops = new Map<number, RevisionLevel>();
  for (const l of levels) {
    const known = stops.get(l.level);
    if (!known || (known.isBase && !l.isBase)) stops.set(l.level, l);
  }
  return [...stops.values()].sort((a, b) => a.level - b.level);
}

/** Stored level if it is a known preset, else the `isDefault` preset, else the base level. */
export function resolveReaderLevel(levels: RevisionLevel[], stored: number | undefined): number {
  if (stored !== undefined && levels.some((l) => l.level === stored)) return stored;
  return levels.find((l) => l.isDefault)?.level ?? BASE_LEVEL;
}

/**
 * Level the reader shows. `ready` turns true once the presets are loaded (or failed), so the chapter is
 * fetched once with the right level instead of first at 100.
 */
export function useReaderLevel() {
  const query = useRevisionLevels();
  const levels = sliderStops(Array.isArray(query.data) ? query.data : []);
  const [stored, setStored] = useState<number | undefined>(readStoredLevel);
  const level = resolveReaderLevel(levels, stored);
  const setLevel = useCallback((next: number) => {
    writeStoredLevel(next);
    setStored(next);
  }, []);
  return { levels, level, setLevel, ready: !query.isPending };
}
