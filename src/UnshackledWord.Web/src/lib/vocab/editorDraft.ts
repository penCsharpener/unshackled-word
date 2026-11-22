import type { MeaningRow } from "@/components/MeaningListEditor";
import {
  addVocabMeaning,
  deleteVocabMeaning,
  reorderVocabMeanings,
  type UpdateVocabEntryBody,
  type UpdateVocabMeaningBody,
  updateVocabEntry,
  updateVocabMeaning,
  type VocabEditorEntryResponse,
} from "./editorApi";

/** What the editor page shows and edits: the entry fields and all meanings in their order. */
export interface EditorDraft {
  dictionaryForm: string;
  transliteration: string;
  reviewed: boolean;
  meanings: MeaningRow[];
}

/** A meaning of the draft in its position: a saved one by id, a new one by its index among the new ones. */
export type MeaningRef = { id: number } | { added: number };

/** The calls that turn the saved entry into the draft, in the order they run. */
export interface SavePlan {
  entry: UpdateVocabEntryBody | null;
  /** Saved meanings the draft dropped. */
  removed: number[];
  /** Meanings without id, in the order of the draft. */
  added: MeaningRow[];
  /** Saved meanings with changed accept, pin or text override. */
  changed: { id: number; body: UpdateVocabMeaningBody }[];
  /** One ref per draft meaning, in the draft order. */
  refs: MeaningRef[];
  /** Saved ids that survive the removals, in their saved order. */
  keptIds: number[];
  /** The saved meanings are in another order than the draft (new meanings are not counted here). */
  orderChanged: boolean;
  /** Changes a learner can notice; a change of the reviewed flag alone is not one. */
  learningRelevant: boolean;
  /** Number of pending changes, for the save bar. */
  count: number;
}

const clean = (value: string) => value.trim();

/** The draft of a loaded entry; the baseline every change is measured against. */
export function draftFromEntry(data: VocabEditorEntryResponse): EditorDraft {
  return {
    dictionaryForm: data.entry.dictionaryForm ?? "",
    transliteration: data.entry.transliteration ?? "",
    reviewed: data.entry.reviewedAt != null,
    meanings: data.meanings.map((meaning) => ({
      id: meaning.id,
      german: meaning.german,
      germanOverride: meaning.germanOverride ?? null,
      count: meaning.count ?? null,
      share: meaning.share ?? null,
      isAccepted: meaning.isAccepted,
      origin: meaning.origin,
      isPinned: meaning.isPinned,
    })),
  };
}

/** Compares the draft with the saved state and lists the calls needed. Pure: no requests. */
export function buildSavePlan(saved: EditorDraft, draft: EditorDraft): SavePlan {
  const formChanged = clean(draft.dictionaryForm) !== clean(saved.dictionaryForm);
  const transliterationChanged = clean(draft.transliteration) !== clean(saved.transliteration);
  const reviewedChanged = draft.reviewed !== saved.reviewed;
  const entry =
    formChanged || transliterationChanged || reviewedChanged
      ? {
          dictionaryForm: clean(draft.dictionaryForm) || null,
          transliteration: clean(draft.transliteration) || null,
          reviewed: draft.reviewed,
        }
      : null;

  const savedIds = saved.meanings.flatMap((row) => (row.id === undefined ? [] : [row.id]));
  const draftIds = new Set(draft.meanings.flatMap((row) => (row.id === undefined ? [] : [row.id])));
  const keptIds = savedIds.filter((id) => draftIds.has(id));
  const removed = savedIds.filter((id) => !draftIds.has(id));

  const savedById = new Map(
    saved.meanings.flatMap((row) => (row.id === undefined ? [] : [[row.id, row] as const])),
  );
  const changed = draft.meanings.flatMap((row) => {
    if (row.id === undefined) return [];
    const before = savedById.get(row.id);
    if (!before) return [];
    const body: UpdateVocabMeaningBody = {};
    if (row.isAccepted !== before.isAccepted) body.isAccepted = row.isAccepted;
    if (row.isPinned !== before.isPinned) body.isPinned = row.isPinned;
    if ((row.germanOverride ?? null) !== (before.germanOverride ?? null)) {
      body.germanOverride = row.germanOverride ?? "";
    }
    return Object.keys(body).length > 0 ? [{ id: row.id, body }] : [];
  });

  const added: MeaningRow[] = [];
  const refs: MeaningRef[] = draft.meanings.map((row) => {
    if (row.id !== undefined) return { id: row.id };
    added.push(row);
    return { added: added.length - 1 };
  });

  const existingOrder = refs.flatMap((ref) => ("id" in ref ? [ref.id] : []));
  const orderChanged = existingOrder.join(",") !== keptIds.join(",");

  const count =
    (entry ? 1 : 0) + removed.length + added.length + changed.length + (orderChanged ? 1 : 0);
  const learningRelevant =
    formChanged ||
    transliterationChanged ||
    removed.length > 0 ||
    added.length > 0 ||
    changed.length > 0 ||
    orderChanged;

  return {
    entry,
    removed,
    added,
    changed,
    refs,
    keptIds,
    orderChanged,
    learningRelevant,
    count,
  };
}

/**
 * Runs the plan against the API, one call after the other. Throws at the first refused call;
 * the calls before it are already saved, so the caller reloads the entry.
 */
export async function saveVocabEntryPlan(entryId: number, plan: SavePlan): Promise<void> {
  if (plan.entry) await updateVocabEntry(entryId, plan.entry);
  for (const id of plan.removed) await deleteVocabMeaning(entryId, id);

  const created: number[] = [];
  for (const row of plan.added) {
    const response = await addVocabMeaning(entryId, row.german);
    if (response.meaningId == null) throw new Error("The new meaning has no id.");
    created.push(response.meaningId);
    // New meanings are accepted and not pinned; only a different state needs a call.
    const body: UpdateVocabMeaningBody = {};
    if (!row.isAccepted) body.isAccepted = false;
    if (row.isPinned) body.isPinned = true;
    if (Object.keys(body).length > 0) await updateVocabMeaning(entryId, response.meaningId, body);
  }

  for (const change of plan.changed) {
    await updateVocabMeaning(entryId, change.id, change.body);
  }

  // The server appends new meanings, so the wanted order is checked after the creations.
  const serverOrder = [...plan.keptIds, ...created];
  const wanted: number[] = [];
  for (const ref of plan.refs) {
    if ("id" in ref) {
      wanted.push(ref.id);
      continue;
    }
    const id = created[ref.added];
    if (id === undefined) throw new Error("A new meaning was not created.");
    wanted.push(id);
  }
  if (wanted.join(",") !== serverOrder.join(",")) {
    await reorderVocabMeanings(entryId, wanted);
  }
}
