import { useCallback, useState } from "react";

/** Reader setting "Notizen anderer anzeigen", kept per viewer in this browser (default on). */
export const SHOW_OTHERS_KEY = "uw.notes.showOthers";

export function readShowOthers(): boolean {
  try {
    return localStorage.getItem(SHOW_OTHERS_KEY) !== "false";
  } catch {
    return true;
  }
}

function writeShowOthers(value: boolean): void {
  try {
    localStorage.setItem(SHOW_OTHERS_KEY, String(value));
  } catch {
    // storage unavailable: the choice only lasts for this page view
  }
}

/** Whether the reader shows other people's notes (drives `others=` of the chapter endpoint). */
export function useShowOthersNotes() {
  const [showOthers, setState] = useState(readShowOthers);
  const setShowOthers = useCallback((value: boolean) => {
    writeShowOthers(value);
    setState(value);
  }, []);
  return { showOthers, setShowOthers };
}
