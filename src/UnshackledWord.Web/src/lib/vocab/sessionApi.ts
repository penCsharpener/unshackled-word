import { api } from "@/lib/api";
import type { VocabLang, VocabSampleVerse } from "./api";

/** Where a session stands; words are the 20-word target (review and new words). Mirrors `VocabSessionProgress`. */
export interface VocabSessionProgress {
  cardsServed: number;
  wordsTotal: number;
  wordsDone: number;
  newWordsTotal: number;
  newWordsIntroduced: number;
  /** Words introduced and not yet passed; they must pass before the session ends. */
  openWave: number;
}

/** The word a card is about. Mirrors `VocabCardEntryResponse`; nulls are omitted by the API. */
export interface VocabCardEntry {
  id: number;
  lemma: string;
  dictionaryForm?: string | null;
  transliteration?: string | null;
  pos?: string | null;
  lang: VocabLang;
  /** Presigned URL, expires after 1 h. */
  audioUrl?: string | null;
}

/**
 * One option of a multiple-choice card; `id` is its position and is sent back as `optionId`. German
 * options have no `lang`; the lemma options of a reverse card carry their script (`grc` or `he`).
 */
export interface VocabCardOption {
  id: number;
  text: string;
  lang?: VocabLang;
}

/** Accepted meaning on an introduction card, in rank order. */
export interface VocabCardMeaning {
  german: string;
  rank: number;
  count?: number | null;
  share?: number | null;
}

/** Kinds the backend serves so far; later quiz kinds extend this union. */
export type VocabCardKind =
  | "introduction"
  | "multiple_choice"
  | "reverse_multiple_choice"
  | "typing";

/** One piece of a typing card's layout: a `group` of `length` letter boxes or a fixed `separator` text. */
export interface VocabTypingPart {
  kind: "group" | "separator";
  length?: number | null;
  text?: string | null;
}

/** The card to show. It never says which option is correct. */
export interface VocabCard {
  /** Timeline position; `answers` sends it back. */
  seq: number;
  kind: VocabCardKind;
  /** Reverse multiple choice: the German meaning the learner reads; the question has no entry data. */
  prompt?: string | null;
  entry: VocabCardEntry;
  meanings?: VocabCardMeaning[] | null;
  options?: VocabCardOption[] | null;
  /** Typing: the German meanings to type the lemma form for, top first; `entry` then has only `id`, `pos`, `lang`. */
  typingPrompt?: string[] | null;
  /** Typing: groups and separators of the target form; `answers` sends one string per group. */
  layout?: VocabTypingPart[] | null;
  verse?: VocabSampleVerse | null;
}

export type VocabStrengthChange = "moved" | "held" | "dropped";

/** Strength (0–5) before and after an answer; `change` compares whole blocks. */
export interface VocabStrength {
  before: number;
  after: number;
  change: VocabStrengthChange;
}

/** One word of the session summary. */
export interface VocabSessionWord {
  entryId: number;
  lemma: string;
  dictionaryForm?: string | null;
  lang: VocabLang;
  isNew: boolean;
  reps: number;
  correct: number;
  strengthBefore: number;
  strengthAfter: number;
  change: VocabStrengthChange;
}

/** Shown when a session is finished. */
export interface VocabSessionSummary {
  newWords: number;
  reviewedWords: number;
  answers: number;
  correctAnswers: number;
  words: VocabSessionWord[];
}

/** Mirrors `POST api/vocab/sessions`. `finished` with sessionId 0 means nothing is due and nothing is new. */
export interface StartVocabSessionResponse {
  sessionId: number;
  course: string;
  resumed: boolean;
  finished: boolean;
  summary?: VocabSessionSummary | null;
  reviewWords: number;
  newWords: number;
  progress: VocabSessionProgress;
  /** Flagged cards of the learner (revised or removed words); the notice shows when above 0. */
  changedCount?: number | null;
}

/** Mirrors `GET api/vocab/sessions/{id}/next`; the card stays the same until it is answered. */
export interface VocabNextResponse {
  sessionId: number;
  finished: boolean;
  progress: VocabSessionProgress;
  card?: VocabCard | null;
  summary?: VocabSessionSummary | null;
}

/** Body of `POST api/vocab/sessions/{id}/answers`; no `optionId` for an introduction. */
export interface SubmitVocabAnswerRequest {
  seq: number;
  optionId?: number;
  /** Typing only: one string per letter group of the card's layout, in order. */
  answers?: string[];
  responseMs?: number;
}

/** Mirrors the answer response: the verdict, the accepted meanings, the strength and one verse. */
export interface VocabAnswerResponse {
  seq: number;
  kind: VocabCardKind;
  correct: boolean;
  correctOptionId?: number | null;
  correctAnswers: string[];
  /** Typing only: per letter group, whether the typed text matched. */
  groupResults?: boolean[] | null;
  given?: string | null;
  entry: VocabCardEntry;
  strength?: VocabStrength | null;
  due?: string | null;
  verse?: VocabSampleVerse | null;
  finished: boolean;
  progress: VocabSessionProgress;
  summary?: VocabSessionSummary | null;
}

/** Starts the session of a course, or resumes its open one (`POST api/vocab/sessions`). */
export function startVocabSession(course: string) {
  return api.post<StartVocabSessionResponse>("/vocab/sessions", { course });
}

/** The next card or the summary (`GET api/vocab/sessions/{id}/next`, no body so no Content-Type). */
export function getVocabSessionNext(sessionId: number, signal?: AbortSignal) {
  return api.get<VocabNextResponse>(`/vocab/sessions/${sessionId}/next`, signal);
}

/** Answers the card with `seq` (`POST api/vocab/sessions/{id}/answers`); 409 means the seq is stale. */
export function submitVocabAnswer(sessionId: number, request: SubmitVocabAnswerRequest) {
  return api.post<VocabAnswerResponse>(`/vocab/sessions/${sessionId}/answers`, request);
}
