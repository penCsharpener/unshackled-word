import { useCallback, useSyncExternalStore } from "react";

export type OriginalLanguage = "greek" | "hebrew";

const key = (language: OriginalLanguage) => `uw.mapping.reverseGerman.${language}`;
const EVENT = "uw:mapping-order";

/** Stored choice; off when unset or storage is unavailable. */
export function readReverseGerman(language: OriginalLanguage): boolean {
  try {
    return localStorage.getItem(key(language)) === "1";
  } catch {
    return false;
  }
}

/** Last choice of this page view; wins over storage, which may be unavailable. */
const memory: Partial<Record<OriginalLanguage, boolean>> = {};

function subscribe(onChange: () => void) {
  /** Another tab changed the choice: storage is the truth again. */
  const fromOtherTab = () => {
    delete memory.greek;
    delete memory.hebrew;
    onChange();
  };
  window.addEventListener(EVENT, onChange);
  window.addEventListener("storage", fromOtherTab);
  return () => {
    window.removeEventListener(EVENT, onChange);
    window.removeEventListener("storage", fromOtherTab);
  };
}

/**
 * Whether the mapping editor shows the German words right to left, so lines to a Hebrew verse cross less.
 * Display only; one choice per original language, shared by every open editor and remembered in this browser.
 */
export function useReverseGerman(language: OriginalLanguage) {
  const reversed = useSyncExternalStore(
    subscribe,
    () => memory[language] ?? readReverseGerman(language),
    () => false,
  );
  const toggle = useCallback(() => {
    const next = !(memory[language] ?? readReverseGerman(language));
    memory[language] = next;
    try {
      localStorage.setItem(key(language), next ? "1" : "0");
    } catch {
      // storage unavailable: the choice only lasts for this page view
    }
    window.dispatchEvent(new Event(EVENT));
  }, [language]);
  return [reversed, toggle] as const;
}
