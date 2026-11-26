// Roadmap 0028.7f: data and draft logic of the LXX review page `/edit/lxx` (design: docs/roadmaps/0028-lxx-rahlfs/06a-review-page.md).
// Types mirror `Domain.WebApi/Maintainer/LxxReviewContracts.cs` (camelCase, nulls omitted).

import {
  keepPreviousData,
  type QueryClient,
  useMutation,
  useQuery,
  useQueryClient,
} from "@tanstack/react-query";
import { useCallback, useState } from "react";
import type { AgentRunBlock } from "@/lib/agent/types";
import { api } from "@/lib/api";
import { invalidateReaderLxx, lxxMaintainerKeys } from "./lxx";

// ---------- contracts ----------

export type LxxReviewTagStatus = "proposed" | "confirmed";
export type LxxReviewTagSource = "nt-form" | "ai" | "human";
export type LxxConfidence = "high" | "low";

export interface LxxReviewChapterCount {
  chapter: number;
  openUnits: number;
  aiUnits: number;
  lowWords: number;
}

export interface LxxReviewBook {
  bibleBookId: number;
  openUnits: number;
  aiUnits: number;
  lowWords: number;
  chapters: LxxReviewChapterCount[];
}

export interface LxxReviewBooksResponse {
  books: LxxReviewBook[];
}

export interface LxxReviewTag {
  lemma: string;
  strongs: string;
  morphology: string;
  status: LxxReviewTagStatus;
  source: LxxReviewTagSource;
}

export interface LxxReviewAiWord {
  lemma: string;
  strongs: string;
  morphology: string;
  confidence: LxxConfidence;
  prefilled: boolean;
  hebrewWordIds: number[];
}

export interface LxxReviewWord {
  id: number;
  position: number;
  greek: string;
  tag?: LxxReviewTag;
  hebrewWordIds: number[];
  linksConfirmed: boolean;
  ai?: LxxReviewAiWord;
}

export interface LxxReviewHebrewWord {
  id: number;
  number: number;
  text: string;
  strongs?: string;
  gloss?: string;
  morphology?: string;
}

export interface LxxReviewFlag {
  code: string;
  message: string;
  position?: number;
}

export interface LxxReviewProposal {
  id: number;
  runId: number;
  createdAt: string;
  note?: string;
  flags: LxxReviewFlag[];
}

export interface LxxReviewUnit {
  lxxVerseId: number;
  chapter: number;
  verse: number;
  subverse: number;
  ref: string;
  /** `og` / `th` in Daniel, else empty (omitted). */
  textForm?: string;
  mapKind: string;
  isAddition: boolean;
  hebRefId?: number;
  open: boolean;
  hasConfirmed: boolean;
  words: LxxReviewWord[];
  hebrew: LxxReviewHebrewWord[];
  proposal?: LxxReviewProposal;
}

export interface LxxReviewChapterResponse {
  bibleBookId: number;
  chapter: number;
  units: LxxReviewUnit[];
}

export interface LxxReviewWordInput {
  lxxWordId: number;
  /** All three or none (none: links only, the stored tag stays). */
  lemma?: string;
  strongs?: string;
  morphology?: string;
  hebrewWordIds: number[];
}

export interface ConfirmLxxUnitRequest {
  lxxVerseId: number;
  proposalId?: number;
  words: LxxReviewWordInput[];
}

export interface ConfirmLxxUnitResponse {
  lxxVerseId: number;
  /** `confirm`, `edit` or empty (nothing changed). */
  action: string;
  confirmedWords: number;
  proposalStatus?: "merged" | "edited-merged";
}

export interface DiscardLxxUnitRequest {
  lxxVerseId: number;
  proposalId: number;
}

export interface DiscardLxxUnitResponse {
  lxxVerseId: number;
  proposalId: number;
  proposalStatus: string;
}

export interface LxxSafeUnitInput {
  lxxVerseId: number;
  proposalId: number;
  lxxWordIds: number[];
}

export interface ConfirmLxxSafeRequest {
  bibleBookId: number;
  chapter: number;
  units: LxxSafeUnitInput[];
}

export interface ConfirmLxxSafeResponse {
  units: number;
  confirmedWords: number;
  skippedLow: number;
  skippedOther: number;
  staleUnits: number[];
  mergedProposals: number;
}

export interface LxxLexiconEntry {
  strongs: string;
  lemma: string;
  gloss: string;
}

export interface SearchLxxLexiconResponse {
  entries: LxxLexiconEntry[];
}

export interface CheckLxxMorphologyResponse {
  code: string;
  valid: boolean;
  error?: string;
  german?: string;
  english?: string;
}

// ---------- query keys ----------

/** Root `["maintainer","lxxReview"]`, NOT under `lxxMaintainerKeys.status` (`["maintainer","lxx"]`), see 06a-review-page.md. */
export const lxxReviewKeys = {
  all: ["maintainer", "lxxReview"] as const,
  books: ["maintainer", "lxxReview", "books"] as const,
  chapter: (bookId: number, chapter: number) =>
    ["maintainer", "lxxReview", "chapter", bookId, chapter] as const,
  lexicon: (q: string) => ["maintainer", "lxxReview", "lexicon", q] as const,
  morphology: (code: string) => ["maintainer", "lxxReview", "morphology", code] as const,
};

const base = "/maintainer/lxx";

// ---------- queries ----------

export function useLxxReviewBooks(enabled: boolean) {
  return useQuery({
    queryKey: lxxReviewKeys.books,
    queryFn: ({ signal }) => api.get<LxxReviewBooksResponse>(`${base}/review/books`, signal),
    select: (response) => response.books,
    enabled,
  });
}

export function useLxxReviewChapter(bookId: number | undefined, chapter: number | undefined) {
  return useQuery({
    queryKey: lxxReviewKeys.chapter(bookId ?? 0, chapter ?? 0),
    queryFn: ({ signal }) =>
      api.get<LxxReviewChapterResponse>(`${base}/review/${bookId}/${chapter}`, signal),
    enabled: bookId !== undefined && chapter !== undefined,
    staleTime: 0,
  });
}

/** TBESG search; `q` is the debounced query, empty or too short disables it (see `lexiconQueryReady`). */
export function useLxxLexicon(q: string) {
  return useQuery({
    queryKey: lxxReviewKeys.lexicon(q),
    queryFn: ({ signal }) =>
      api.get<SearchLxxLexiconResponse>(
        `${base}/lexicon?q=${encodeURIComponent(q)}&limit=20`,
        signal,
      ),
    select: (response) => response.entries,
    enabled: lexiconQueryReady(q),
    staleTime: 5 * 60_000,
    placeholderData: keepPreviousData,
  });
}

/** Live parse-code check; `code` is the debounced, trimmed code. The parser is static, so results never go stale. */
export function useLxxMorphologyCheck(code: string) {
  return useQuery({
    queryKey: lxxReviewKeys.morphology(code),
    queryFn: ({ signal }) =>
      api.get<CheckLxxMorphologyResponse>(
        `${base}/morphology?code=${encodeURIComponent(code)}`,
        signal,
      ),
    enabled: code.trim() !== "",
    staleTime: Number.POSITIVE_INFINITY,
    retry: false,
  });
}

// ---------- mutations ----------

/** Everything a review write makes stale: this chapter, the book list, the Wartung status and the reader's LXX caches. */
export function invalidateAfterReview(queryClient: QueryClient, bookId: number, chapter: number) {
  return Promise.all([
    queryClient.invalidateQueries({ queryKey: lxxReviewKeys.chapter(bookId, chapter) }),
    queryClient.invalidateQueries({ queryKey: lxxReviewKeys.books }),
    queryClient.invalidateQueries({ queryKey: lxxMaintainerKeys.status, exact: true }),
    invalidateReaderLxx(queryClient),
  ]);
}

/** The caller keeps a per-unit save state; do not drive card state from the shared `isPending`. */
export function useConfirmLxxUnit(bookId: number, chapter: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: ConfirmLxxUnitRequest) =>
      api.post<ConfirmLxxUnitResponse>(`${base}/review/confirm`, body),
    // Not awaited: the caller sets the card state first, then the refetch reconciles the draft (`reconcileDraft`).
    onSettled: () => {
      void invalidateAfterReview(queryClient, bookId, chapter);
    },
  });
}

export function useDiscardLxxUnit(bookId: number, chapter: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: DiscardLxxUnitRequest) =>
      api.post<DiscardLxxUnitResponse>(`${base}/review/discard`, body),
    // Not awaited: the caller sets the card state first, then the refetch reconciles the draft (`reconcileDraft`).
    onSettled: () => {
      void invalidateAfterReview(queryClient, bookId, chapter);
    },
  });
}

export function useConfirmLxxSafe(bookId: number, chapter: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: ConfirmLxxSafeRequest) =>
      api.post<ConfirmLxxSafeResponse>(`${base}/review/confirm-chapter-safe`, body),
    // Not awaited: the caller sets the card state first, then the refetch reconciles the draft (`reconcileDraft`).
    onSettled: () => {
      void invalidateAfterReview(queryClient, bookId, chapter);
    },
  });
}

// ---------- drafts (pure) ----------

export interface WordDraft {
  lemma: string;
  strongs: string;
  morphology: string;
  hebrewWordIds: number[];
}

export type UnitSaveState = "idle" | "saving" | "saved" | "error" | "conflict";

export interface UnitDraft {
  /** The proposal id the draft was built from (what confirm/discard send). */
  proposalId?: number;
  words: Map<number, WordDraft>;
  saveState: UnitSaveState;
  error?: string;
}

/** i18n keys under `lxxReview.error`. */
export type WordDraftError = "partial" | "strongs" | "morphology";

export const STRONGS_PATTERN = /^G\d{4,5}[A-Z]?$/;

/** Strong's number (`G7`, `g0746`) needs one digit, a lemma start two characters. */
export function lexiconQueryReady(q: string): boolean {
  const trimmed = q.trim();
  return /^[gG]\d/.test(trimmed) || trimmed.length >= 2;
}

/** AI answer, else stored tag + links, else empty + stored links (06a "Draft model"). */
export function initialWordDraft(word: LxxReviewWord): WordDraft {
  if (word.ai) {
    return {
      lemma: word.ai.lemma,
      strongs: word.ai.strongs,
      morphology: word.ai.morphology,
      hebrewWordIds: [...word.ai.hebrewWordIds],
    };
  }
  return {
    lemma: word.tag?.lemma ?? "",
    strongs: word.tag?.strongs ?? "",
    morphology: word.tag?.morphology ?? "",
    hebrewWordIds: [...word.hebrewWordIds],
  };
}

export function initialUnitDraft(unit: LxxReviewUnit): UnitDraft {
  return {
    proposalId: unit.proposal?.id,
    words: new Map(unit.words.map((word) => [word.id, initialWordDraft(word)])),
    saveState: "idle",
  };
}

export function sameWordDraft(a: WordDraft, b: WordDraft): boolean {
  return (
    a.lemma === b.lemma &&
    a.strongs === b.strongs &&
    a.morphology === b.morphology &&
    a.hebrewWordIds.length === b.hebrewWordIds.length &&
    a.hebrewWordIds.every((id, i) => id === b.hebrewWordIds[i])
  );
}

/** True when any word differs from its initial draft. */
export function isUnitDirty(unit: LxxReviewUnit, draft: UnitDraft | undefined): boolean {
  if (!draft) return false;
  return unit.words.some((word) => {
    const current = draft.words.get(word.id);
    return current !== undefined && !sameWordDraft(current, initialWordDraft(word));
  });
}

/** Client-side checks mirroring the contract; `morphologyValid` is the live check's answer (undefined while it loads or unchecked). */
export function validateWordDraft(
  draft: WordDraft,
  morphologyValid: boolean | undefined,
): WordDraftError[] {
  const filled = [draft.lemma, draft.strongs, draft.morphology].filter((v) => v.trim() !== "");
  if (filled.length === 0) return [];
  const errors: WordDraftError[] = [];
  if (filled.length !== 3) errors.push("partial");
  if (draft.strongs.trim() !== "" && !STRONGS_PATTERN.test(draft.strongs.trim())) {
    errors.push("strongs");
  }
  if (draft.morphology.trim() !== "" && morphologyValid === false) errors.push("morphology");
  return errors;
}

/** Hebrew ids of a draft that exist in the unit, in Hebrew reading order (an id can only vanish after a refetch). */
export function sortedHebrewIds(unit: LxxReviewUnit, ids: readonly number[]): number[] {
  const wanted = new Set(ids);
  return unit.hebrew.filter((h) => wanted.has(h.id)).map((h) => h.id);
}

/** Body of "Vers bestätigen": every word of the unit, tag fields only when all three are filled (code upper-cased). */
export function buildConfirmRequest(unit: LxxReviewUnit, draft: UnitDraft): ConfirmLxxUnitRequest {
  return {
    lxxVerseId: unit.lxxVerseId,
    ...(draft.proposalId !== undefined ? { proposalId: draft.proposalId } : {}),
    words: unit.words.map((word) => {
      const d = draft.words.get(word.id) ?? initialWordDraft(word);
      const lemma = d.lemma.trim();
      const strongs = d.strongs.trim().toUpperCase();
      const morphology = d.morphology.trim().toUpperCase();
      const tagged = lemma !== "" && strongs !== "" && morphology !== "";
      return {
        lxxWordId: word.id,
        ...(tagged ? { lemma, strongs, morphology } : {}),
        hebrewWordIds: sortedHebrewIds(unit, d.hebrewWordIds),
      };
    }),
  };
}

export interface SafeSelection {
  units: LxxSafeUnitInput[];
  counts: {
    /** Words that will be sent (AI `high`, unconfirmed, unchanged). */
    words: number;
    units: number;
    /** AI-`low` unconfirmed words on screen; they stay open. */
    low: number;
    /** Visible units with a proposal that are left out because of unsaved edits or a conflict. */
    dirty: number;
  };
}

/**
 * "Kapitel bestätigen (sichere)" (06a): only units on screen (`visibleIds`) with a proposal, no dirty draft and no conflict;
 * of those the unconfirmed words with AI `high`. A `low` word and an nt-form prefill alone never count as safe.
 */
export function safeSelection(
  units: readonly LxxReviewUnit[],
  drafts: ReadonlyMap<number, UnitDraft>,
  visibleIds: ReadonlySet<number>,
): SafeSelection {
  const result: SafeSelection = { units: [], counts: { words: 0, units: 0, low: 0, dirty: 0 } };
  for (const unit of units) {
    if (!visibleIds.has(unit.lxxVerseId) || !unit.proposal) continue;
    const draft = drafts.get(unit.lxxVerseId);
    result.counts.low += unit.words.filter(
      (w) => w.ai?.confidence === "low" && w.tag?.status !== "confirmed",
    ).length;
    if (isUnitDirty(unit, draft) || draft?.saveState === "conflict") {
      result.counts.dirty += 1;
      continue;
    }
    const ids = unit.words
      .filter((w) => w.ai?.confidence === "high" && w.tag?.status !== "confirmed")
      .map((w) => w.id);
    if (ids.length === 0) continue;
    result.units.push({
      lxxVerseId: unit.lxxVerseId,
      proposalId: unit.proposal.id,
      lxxWordIds: ids,
    });
    result.counts.units += 1;
    result.counts.words += ids.length;
  }
  return result;
}

export function safeUnits(
  units: readonly LxxReviewUnit[],
  drafts: ReadonlyMap<number, UnitDraft>,
  visibleIds: ReadonlySet<number>,
): LxxSafeUnitInput[] {
  return safeSelection(units, drafts, visibleIds).units;
}

export interface SelectionRuns {
  /** First block as `AgentRunDialog.initial` (the dialog needs a book). */
  initial: {
    book: number;
    fromChapter: number;
    fromVerse: number;
    toChapter: number;
    toVerse: number;
  };
  /** One entry per contiguous block of verses (Rahlfs numbering, inclusive); never contains an unselected verse. */
  blocks: AgentRunBlock[];
  /** Selected units with confirmed tags (skipped by the run unless "auch bestätigte" is set). */
  confirmedUnits: number;
}

/**
 * The runs for "KI neu für Auswahl": a verse is selected when one of its units is (subverses and the Daniel forms of that verse
 * come with it, a text form filter is sent as `textForm`). Selected verses that follow each other in the chapter's verse list form
 * one block; a gap starts the next block, so no unselected verse is ever included.
 */
export function selectionRange(
  bookId: number,
  units: readonly LxxReviewUnit[],
  selectedIds: ReadonlySet<number>,
): SelectionRuns | null {
  const selectedUnits = units.filter((u) => selectedIds.has(u.lxxVerseId));
  if (selectedUnits.length === 0) return null;
  const chapter = selectedUnits[0]?.chapter ?? 0;
  const selectedVerses = new Set(selectedUnits.map((u) => u.verse));
  const verses = [...new Set(units.map((u) => u.verse))].sort((a, b) => a - b);
  const blocks: AgentRunBlock[] = [];
  let open: AgentRunBlock | undefined;
  for (const verse of verses) {
    if (!selectedVerses.has(verse)) {
      open = undefined;
      continue;
    }
    if (open) {
      open.toVerse = verse;
    } else {
      open = { fromChapter: chapter, fromVerse: verse, toChapter: chapter, toVerse: verse };
      blocks.push(open);
    }
  }
  const first = blocks[0] as AgentRunBlock;
  return {
    initial: { book: bookId, ...first },
    blocks,
    confirmedUnits: selectedUnits.filter((u) => u.hasConfirmed).length,
  };
}

// ---------- drafts (state) ----------

/** What a unit looked like when its draft was built: the proposal id and every word's stored/AI state. */
function unitSignature(unit: LxxReviewUnit): string {
  return JSON.stringify([
    unit.proposal?.id ?? null,
    unit.words.map((w) => [w.id, w.tag, w.hebrewWordIds, w.linksConfirmed, w.ai]),
  ]);
}

interface DraftState {
  units: readonly LxxReviewUnit[] | undefined;
  drafts: Map<number, UnitDraft>;
  /** Server state each draft was built from (to tell edits from server changes). */
  base: Map<number, LxxReviewUnit>;
}

/**
 * A unit's draft after a refetch: unchanged server state keeps the draft; changed state resets it, except when it holds unsaved
 * edits (then `conflict`, the draft stays). A reset keeps a running or finished save (`saving` / `saved`).
 */
export function reconcileDraft(
  previous: UnitDraft | undefined,
  previousUnit: LxxReviewUnit | undefined,
  next: LxxReviewUnit,
): UnitDraft {
  if (!previous || !previousUnit) return initialUnitDraft(next);
  if (unitSignature(previousUnit) === unitSignature(next)) return previous;
  const fresh = initialUnitDraft(next);
  // Our own save (or one that just finished) made the change: follow the server.
  if (previous.saveState === "saving" || previous.saveState === "saved") {
    return { ...fresh, saveState: previous.saveState };
  }
  const edited = isUnitDirty(previousUnit, previous);
  return edited && isUnitDirty(next, previous) ? { ...previous, saveState: "conflict" } : fresh;
}

/**
 * Per-unit drafts keyed by `lxxVerseId`. They follow the server through `reconcileDraft`; the setters are no-ops for an unknown
 * unit. Editing a word clears a `saved` / `error` mark.
 */
export function useUnitDrafts(units: readonly LxxReviewUnit[] | undefined): {
  drafts: ReadonlyMap<number, UnitDraft>;
  updateWord: (lxxVerseId: number, lxxWordId: number, patch: Partial<WordDraft>) => void;
  toggleHebrew: (lxxVerseId: number, lxxWordId: number, hebrewWordId: number) => void;
  resetWord: (lxxVerseId: number, lxxWordId: number) => void;
  resetUnit: (lxxVerseId: number) => void;
  setSaveState: (lxxVerseId: number, state: UnitSaveState, error?: string) => void;
} {
  const [state, setState] = useState<DraftState>({
    units: undefined,
    drafts: new Map(),
    base: new Map(),
  });
  let current = state;
  if (units !== state.units) {
    // Derived from props during render (no flash of unit cards without a draft).
    const drafts = new Map<number, UnitDraft>();
    const base = new Map<number, LxxReviewUnit>();
    for (const unit of units ?? []) {
      const previous = state.drafts.get(unit.lxxVerseId);
      const draft = reconcileDraft(previous, state.base.get(unit.lxxVerseId), unit);
      drafts.set(unit.lxxVerseId, draft);
      base.set(unit.lxxVerseId, unit);
    }
    current = { units, drafts, base };
    setState(current);
  }

  const change = useCallback(
    (lxxVerseId: number, fn: (draft: UnitDraft, unit: LxxReviewUnit) => UnitDraft) =>
      setState((prev) => {
        const draft = prev.drafts.get(lxxVerseId);
        const unit = prev.units?.find((u) => u.lxxVerseId === lxxVerseId);
        if (!draft || !unit) return prev;
        const drafts = new Map(prev.drafts);
        drafts.set(lxxVerseId, fn(draft, unit));
        return { ...prev, drafts };
      }),
    [],
  );

  const editing = (draft: UnitDraft, words: Map<number, WordDraft>): UnitDraft => ({
    ...draft,
    words,
    saveState:
      draft.saveState === "saved" || draft.saveState === "error" ? "idle" : draft.saveState,
    error: undefined,
  });

  return {
    drafts: current.drafts,
    updateWord: (lxxVerseId, lxxWordId, patch) =>
      change(lxxVerseId, (draft) => {
        const word = draft.words.get(lxxWordId);
        if (!word) return draft;
        return editing(draft, new Map(draft.words).set(lxxWordId, { ...word, ...patch }));
      }),
    toggleHebrew: (lxxVerseId, lxxWordId, hebrewWordId) =>
      change(lxxVerseId, (draft, unit) => {
        const word = draft.words.get(lxxWordId);
        if (!word || !unit.hebrew.some((h) => h.id === hebrewWordId)) return draft;
        const ids = word.hebrewWordIds.includes(hebrewWordId)
          ? word.hebrewWordIds.filter((id) => id !== hebrewWordId)
          : [...word.hebrewWordIds, hebrewWordId];
        return editing(
          draft,
          new Map(draft.words).set(lxxWordId, {
            ...word,
            hebrewWordIds: sortedHebrewIds(unit, ids),
          }),
        );
      }),
    resetWord: (lxxVerseId, lxxWordId) =>
      change(lxxVerseId, (draft, unit) => {
        const word = unit.words.find((w) => w.id === lxxWordId);
        if (!word) return draft;
        return editing(draft, new Map(draft.words).set(lxxWordId, initialWordDraft(word)));
      }),
    resetUnit: (lxxVerseId) =>
      setState((prev) => {
        const target = prev.units?.find((u) => u.lxxVerseId === lxxVerseId);
        if (!target) return prev;
        const drafts = new Map(prev.drafts).set(lxxVerseId, initialUnitDraft(target));
        const base = new Map(prev.base).set(lxxVerseId, target);
        return { ...prev, drafts, base };
      }),
    setSaveState: (lxxVerseId, saveState, error) =>
      change(lxxVerseId, (draft) => ({ ...draft, saveState, error })),
  };
}
