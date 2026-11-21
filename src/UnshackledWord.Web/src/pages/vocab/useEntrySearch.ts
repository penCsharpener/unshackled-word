import { useQuery } from "@tanstack/react-query";
import { useDebouncedValue } from "@/lib/useDebouncedValue";
import {
  getVocabEditorList,
  type VocabEditorListItem,
  vocabEditorKeys,
} from "@/lib/vocab/editorApi";

/**
 * Entries matching a typed lemma, transliteration or German text, for the target pickers of the
 * key move and merge dialogs. The entry being edited is left out; only active entries are offered.
 */
export function useEntrySearch(
  course: string | undefined,
  text: string,
  excludeId: number,
  enabled: boolean,
) {
  const term = useDebouncedValue(text.trim(), 300);
  const query = useQuery({
    queryKey: vocabEditorKeys.search(course, term),
    queryFn: ({ signal }) => getVocabEditorList({ course, q: term, skip: 0, take: 10 }, signal),
    enabled: enabled && term.length > 0,
  });
  const items: VocabEditorListItem[] = (query.data?.items ?? []).filter(
    (item) => item.id !== excludeId,
  );
  return { term, items, isError: query.isError };
}
