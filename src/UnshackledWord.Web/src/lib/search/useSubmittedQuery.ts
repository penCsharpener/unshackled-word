import { useCallback, useEffect, useState } from "react";

export const SEARCH_DEBOUNCE_MS = 300;
export const SEARCH_MIN_CHARS = 2;

const normalize = (input: string) => input.trim().replace(/\s+/g, " ");
const searchable = (input: string) =>
  normalize(input).length >= SEARCH_MIN_CHARS ? normalize(input) : "";

/**
 * The query to send for what is typed: after a space at once, otherwise 300 ms after the last
 * keystroke, never below 2 characters (then the query is empty). `submit` (Enter) commits at once.
 */
export function useSubmittedQuery(input: string) {
  const [query, setQuery] = useState(() => searchable(input));

  useEffect(() => {
    const next = searchable(input);
    if (next === "" || /\s$/.test(input)) {
      setQuery(next);
      return;
    }
    const timer = setTimeout(() => setQuery(next), SEARCH_DEBOUNCE_MS);
    return () => clearTimeout(timer);
  }, [input]);

  const submit = useCallback(() => setQuery(searchable(input)), [input]);
  return { query, submit };
}
