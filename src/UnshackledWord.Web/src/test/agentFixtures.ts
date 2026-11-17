import { QueryClient } from "@tanstack/react-query";
import { vi } from "vitest";
import type {
  AgentProfile,
  AgentProposalDiff,
  AgentProposalSummary,
  AgentRun,
  AgentRunEstimate,
} from "@/lib/agent/types";

/** A client that never retries, so a failing stub shows up at once. */
export const testQueryClient = () =>
  new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });

export const profiles: AgentProfile[] = [
  {
    name: "ollama-cloud",
    kind: "openai",
    model: "gemma4:31b",
    usable: true,
    maxTokensPerRun: 2_000_000,
    maxCostPerRun: 5,
    maxParallel: 2,
  },
  {
    name: "claude",
    kind: "anthropic",
    model: "claude-sonnet",
    usable: false,
    unusableReason: "kein API-Schlüssel",
    maxParallel: 1,
  },
];

export const estimate: AgentRunEstimate = {
  profile: "ollama-cloud",
  model: "gemma4:31b",
  promptVersion: "greek-v1",
  mode: "redo",
  packages: 3,
  verses: 45,
  skippedVerses: 2,
  prefixTokens: 4000,
  estimatedInputTokens: 120_000,
  estimatedCachedInputTokens: 80_000,
  estimatedOutputTokens: 30_000,
  estimatedTotalTokens: 195_000,
  estimatedCostUsd: 0.4321,
  maxTokens: 2_000_000,
  maxCostUsd: 5,
  exceedsCap: false,
  stats: {
    germanTokens: 800,
    removedPunctuation: 90,
    fixed: 200,
    hint: 400,
    open: 200,
    lemmaFixed: 10,
    lemmaHint: 10,
    lemmaOpen: 10,
    verses: 47,
    skippedVerses: 2,
  },
  warnings: [],
};

export const run: AgentRun = {
  id: 7,
  profile: "ollama-cloud",
  model: "gemma4:31b",
  promptVersion: "greek-v1",
  book: 41,
  fromRefId: 41001001,
  toRefId: 41001045,
  mode: "redo",
  status: "running",
  inputTokens: 50_000,
  cachedInputTokens: 30_000,
  outputTokens: 10_000,
  costUsd: 0.12,
  cacheRate: 0.6,
  estimatedTokens: 195_000,
  estimatedCostUsd: 0.4321,
  maxTokens: 2_000_000,
  maxCostUsd: 5,
  createdAt: "2026-11-16T10:00:00Z",
  startedAt: "2026-11-16T10:00:05Z",
  packages: 3,
  packagesCompleted: 1,
  packagesFailed: 0,
  proposedVerses: 15,
  flaggedVerses: 2,
  openProposals: 15,
  active: true,
};

export type Handler = (body: unknown) => Response;

/** Stubs fetch; handlers are keyed "METHOD /path" (without /api) and receive the parsed JSON body. */
export function stubFetch(handlers: Record<string, Handler>) {
  const mock = vi.fn(async (url: string, init?: RequestInit) => {
    const key = `${init?.method ?? "GET"} ${url.replace(/^\/api/, "")}`;
    const body = init?.body ? JSON.parse(init.body as string) : undefined;
    return handlers[key]?.(body) ?? new Response("", { status: 404 });
  });
  vi.stubGlobal("fetch", mock);
  return mock;
}

/** Parsed JSON bodies of the calls made to "METHOD /path". */
export const bodiesOf = (mock: ReturnType<typeof stubFetch>, key: string) =>
  mock.mock.calls
    .filter(
      ([url, init]) => `${init?.method ?? "GET"} ${String(url).replace(/^\/api/, "")}` === key,
    )
    .map(([, init]) => (init?.body ? JSON.parse(init.body as string) : undefined));

/** Mark 1,8: live has "habe getauft" linked to the wrong word; the proposal fixes it and adds "euch". */
export const proposalDiff: AgentProposalDiff = {
  id: 5,
  runId: 7,
  profile: "ollama-cloud",
  model: "gemma4:31b",
  aiUserId: 77,
  book: 41,
  chapter: 1,
  verse: 8,
  language: "greek",
  status: "open",
  note: "„euch“ steht für ὑμᾶς.",
  stale: false,
  germanWords: [
    { id: 101, positionInVerse: 1, text: "Ich" },
    { id: 103, positionInVerse: 2, text: "euch" },
    { id: 104, positionInVerse: 3, text: "getauft" },
  ],
  originalWords: [
    { id: 201, position: 1, original: "ἐγὼ" },
    { id: 202, position: 2, original: "ἐβάπτισα" },
    { id: 203, position: 3, original: "ὑμᾶς" },
  ],
  unchanged: [
    { elbWordId: 101, german: "Ich", stepWordId: 201, original: "ἐγὼ", isAddedWord: false },
  ],
  added: [
    {
      elbWordId: 104,
      german: "getauft",
      stepWordId: 202,
      original: "ἐβάπτισα",
      isAddedWord: false,
    },
    { elbWordId: 103, german: "euch", stepWordId: 203, original: "ὑμᾶς", isAddedWord: false },
  ],
  removed: [
    { elbWordId: 104, german: "getauft", stepWordId: 203, original: "ὑμᾶς", isAddedWord: false },
  ],
  proposedLinks: [
    { elbWordId: 101, stepWordId: 201, isAddedWord: false },
    { elbWordId: 104, stepWordId: 202, isAddedWord: false },
    { elbWordId: 103, stepWordId: 203, isAddedWord: false },
  ],
  lemmaChanges: [
    {
      elbWordId: 104,
      german: "getauft",
      currentLemma: "getauft",
      beforeLemma: "getauft",
      lemma: "taufen",
      stts: "VVINF",
      partOfSpeech: "VERB",
      changed: false,
    },
  ],
  flags: [
    {
      code: "many-to-one",
      message: "8 ἐβάπτισα: 2 German words share this source word",
      stepWordId: 202,
    },
  ],
};

/** Lemma mode (roadmap 0018.8): „Und er sprach“ – וַיֹּאמֶר; `Und` moves to the H9001 prefix, `er` becomes an added word of `sprach`. */
export const lemmaProposalDiff: AgentProposalDiff = {
  id: 5,
  runId: 8,
  profile: "ollama-cloud",
  model: "gemma4:31b",
  aiUserId: 77,
  book: 41,
  chapter: 1,
  verse: 3,
  language: "hebrew",
  status: "open",
  stale: false,
  germanWords: [
    { id: 301, positionInVerse: 1, text: "Und" },
    { id: 302, positionInVerse: 2, text: "er" },
    { id: 303, positionInVerse: 3, text: "sprach" },
  ],
  originalWords: [
    {
      id: 401,
      position: 1,
      original: "וַיֹּאמֶר",
      gloss: "and he said",
      morphemes: [
        { strongs: "H9001", isMain: false, original: "וַ", gloss: "and" },
        { strongs: "H0559", isMain: true, original: "יֹּאמֶר", gloss: "to say" },
      ],
    },
  ],
  unchanged: [
    { elbWordId: 301, german: "Und", stepWordId: 401, original: "וַיֹּאמֶר", isAddedWord: false },
    { elbWordId: 303, german: "sprach", stepWordId: 401, original: "וַיֹּאמֶר", isAddedWord: false },
  ],
  added: [
    {
      elbWordId: 302,
      german: "er",
      isAddedWord: true,
      parentElbWordId: 303,
      parentGerman: "sprach",
    },
  ],
  removed: [
    { elbWordId: 302, german: "er", stepWordId: 401, original: "וַיֹּאמֶר", isAddedWord: false },
  ],
  currentLinks: [
    { elbWordId: 301, stepWordId: 401, isAddedWord: false, role: "head" },
    { elbWordId: 302, stepWordId: 401, isAddedWord: false, role: "head" },
    { elbWordId: 303, stepWordId: 401, isAddedWord: false, role: "head" },
  ],
  proposedLinks: [
    { elbWordId: 301, stepWordId: 401, isAddedWord: false, role: "head", strongsNumber: "H9001" },
    { elbWordId: 303, stepWordId: 401, isAddedWord: false, role: "head" },
    { elbWordId: 302, isAddedWord: true, parentElbWordId: 303 },
  ],
  lemmaChanges: [],
  roleChanges: [
    {
      elbWordId: 301,
      german: "Und",
      stepWordId: 401,
      original: "וַיֹּאמֶר",
      role: "head",
      strongsNumber: "H9001",
      multiHead: false,
      beforeRole: "head",
      beforeMultiHead: false,
      source: "rules",
    },
    {
      elbWordId: 302,
      german: "er",
      stepWordId: 401,
      original: "וַיֹּאמֶר",
      multiHead: false,
      beforeRole: "head",
      beforeMultiHead: false,
      addedParentElbWordId: 303,
      addedParentGerman: "sprach",
      source: "model",
    },
  ],
  flags: [
    { code: "pronoun-added", message: "er: subject pronoun becomes an added word", elbWordId: 302 },
  ],
};

export const summary = (
  id: number,
  verse: number,
  overrides: Partial<AgentProposalSummary> = {},
): AgentProposalSummary => ({
  id,
  runId: 7,
  book: 41,
  chapter: 1,
  verse,
  status: "open",
  flagCount: 0,
  hasLemmaChanges: false,
  hasParts: false,
  hasNote: false,
  createdAt: "2026-11-16T10:05:00Z",
  ...overrides,
});

export const markBooks = {
  books: [
    {
      id: 41,
      testament: "NT",
      nameDe: "Markus",
      nameEn: "Mark",
      abbreviationDe: "Mk",
      abbreviationEn: "Mk",
      versesPerChapter: [45],
    },
  ],
};
