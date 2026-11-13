import { useEffect, useRef } from "react";

export function isTypingTarget(target: EventTarget | null): boolean {
  if (!(target instanceof HTMLElement)) return false;
  return (
    target instanceof HTMLInputElement ||
    target instanceof HTMLTextAreaElement ||
    target instanceof HTMLSelectElement ||
    target.isContentEditable
  );
}

function popoverOpen(): boolean {
  try {
    return document.querySelector(":popover-open") !== null;
  } catch {
    return false; // engines without the Popover API
  }
}

/**
 * `F` (no Ctrl/Alt/Meta) calls `onOpen`, unless the user is typing, a mapping editor, a dialog or
 * the word popover is open, or the key was handled already. Same guard as the arrow keys of the reader.
 */
export function useSearchHotkey(enabled: boolean, onOpen: () => void) {
  const open = useRef(onOpen);
  open.current = onOpen;

  useEffect(() => {
    if (!enabled) return;
    function onKeyDown(event: KeyboardEvent) {
      if (event.key !== "f" && event.key !== "F") return;
      if (event.defaultPrevented || event.repeat) return;
      if (event.altKey || event.ctrlKey || event.metaKey) return;
      if (isTypingTarget(event.target)) return;
      if (event.target instanceof Element && event.target.closest("[data-mapping-editor]")) return;
      if (document.querySelector("dialog[open]") || popoverOpen()) return;
      event.preventDefault();
      open.current();
    }
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, [enabled]);
}
