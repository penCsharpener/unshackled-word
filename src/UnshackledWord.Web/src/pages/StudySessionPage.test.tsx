import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router";
import { afterEach, describe, expect, it, vi } from "vitest";
import "@/i18n";
import { AuthProvider } from "@/lib/auth/AuthContext";
import { json, me } from "@/test/authHelpers";
import { fixtureStructure } from "@/test/fixtures/bibleStructure";
import { StudySessionPage } from "./StudySessionPage";

const entry = {
  id: 56,
  lemma: "λόγος",
  dictionaryForm: "λόγος, -ου, ὁ",
  transliteration: "logos",
  pos: "Substantiv",
  lang: "grc",
};

const verse = {
  bibleBookId: 43,
  chapter: 1,
  verse: 1,
  fromRevision: true,
  humanMade: false,
  original: [
    { position: 1, text: "Ἐν", isHit: false },
    { position: 5, text: "λόγος", isHit: true },
  ],
  german: [
    { position: 1, text: "Im", isHit: false },
    { position: 4, text: "Wort", isHit: true },
  ],
};

const progress = (wordsDone: number, cardsServed: number) => ({
  cardsServed,
  wordsTotal: 20,
  wordsDone,
  stepsDone: wordsDone * 3,
  stepsTotal: 80,
  newWordsTotal: 1,
  newWordsIntroduced: wordsDone > 0 ? 1 : 0,
  openWave: wordsDone > 0 ? 0 : 1,
});

const introductionCard = {
  seq: 1,
  kind: "introduction",
  entry,
  meanings: [
    { german: "Wort", rank: 1, count: 220 },
    { german: "Rede", rank: 2, count: 40 },
  ],
  verse,
  root: {
    id: 1,
    root: "πορεύω",
    strongsKey: "G4198",
    prefixes: "ἐκ",
    origin: "prefill",
    isSimple: false,
  },
};

const choiceCard = {
  seq: 2,
  kind: "multiple_choice",
  entry,
  options: [
    { id: 0, text: "Gesetz" },
    { id: 1, text: "Wort" },
    { id: 2, text: "Geist" },
    { id: 3, text: "Tag" },
  ],
};

const summary = {
  newWords: 1,
  reviewedWords: 0,
  answers: 2,
  correctAnswers: 1,
  words: [
    {
      entryId: 56,
      lemma: "λόγος",
      dictionaryForm: "λόγος, -ου, ὁ",
      lang: "grc",
      isNew: true,
      reps: 2,
      correct: 1,
      strengthBefore: 0,
      strengthAfter: 1,
      change: "moved",
    },
  ],
};

const started = {
  sessionId: 7,
  course: "nt-greek",
  resumed: false,
  finished: false,
  reviewWords: 0,
  newWords: 1,
  progress: progress(0, 0),
};

/**
 * A scripted server: the session walks introduction, multiple choice, finished. The answer to the
 * multiple-choice card is correct for option 1 ("Wort") and a drop for anything else.
 */
function scriptedApi(
  options: { nothingDue?: boolean; changedCount?: number; roles?: string[] } = {},
) {
  let phase: "introduction" | "choice" | "done" = "introduction";
  const fetchMock = vi.fn(async (url: string, init?: RequestInit) => {
    const path = url.replace(/^\/api/, "");
    const method = init?.method ?? "GET";
    if (path === "/auth/me") return json({ ...me, roles: options.roles ?? [] });
    if (path === "/meta-bible/structure") return json(fixtureStructure);
    if (method === "POST" && path === "/vocab/sessions") {
      const changedCount = options.changedCount ?? 0;
      return options.nothingDue
        ? json({ ...started, sessionId: 0, finished: true, newWords: 0, changedCount })
        : json({ ...started, changedCount });
    }
    // Coverage is 61.1 % before the session and 62.4 % once the summary is reached.
    if (method === "GET" && path === "/vocab/courses") {
      const coverage = phase === "done" ? 62.4 : 61.1;
      return json({
        courses: [
          {
            code: "nt-greek",
            track: "G",
            nameDe: "NT",
            nameEn: "NT",
            totalEntries: 5407,
            totalOccurrences: 141774,
            coverage,
          },
        ],
      });
    }
    if (method === "GET" && path === "/vocab/changes") {
      return json({ count: options.changedCount ?? 0, items: [] });
    }
    if (method === "GET" && path === "/vocab/sessions/7/next") {
      if (phase === "introduction") {
        return json({
          sessionId: 7,
          finished: false,
          progress: progress(0, 0),
          card: introductionCard,
        });
      }
      if (phase === "choice") {
        return json({ sessionId: 7, finished: false, progress: progress(0, 1), card: choiceCard });
      }
      return json({ sessionId: 7, finished: true, progress: progress(1, 2), summary });
    }
    if (method === "POST" && path === "/vocab/sessions/7/answers") {
      const body = JSON.parse(String(init?.body)) as { seq: number; optionId?: number };
      if (body.seq === 1) {
        phase = "choice";
        return json({
          seq: 1,
          kind: "introduction",
          correct: true,
          correctAnswers: ["Wort", "Rede"],
          entry,
          finished: false,
          progress: progress(0, 1),
        });
      }
      phase = "done";
      const right = body.optionId === 1;
      return json({
        seq: 2,
        kind: "multiple_choice",
        correct: right,
        correctOptionId: 1,
        correctAnswers: ["Wort", "Rede"],
        given: ["Gesetz", "Wort", "Geist", "Tag"][body.optionId ?? 0],
        entry,
        strength: right
          ? { before: 2, after: 3, change: "moved" }
          : { before: 2, after: 1, change: "dropped" },
        verse,
        finished: true,
        progress: progress(1, 2),
        summary,
      });
    }
    return new Response("", { status: 404 });
  });
  vi.stubGlobal("fetch", fetchMock);
  return fetchMock;
}

function renderSession() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <AuthProvider>
        <MemoryRouter initialEntries={["/learn/nt-greek"]}>
          <Routes>
            <Route path="/learn/:course" element={<StudySessionPage />} />
          </Routes>
        </MemoryRouter>
      </AuthProvider>
    </QueryClientProvider>,
  );
}

afterEach(() => {
  vi.unstubAllGlobals();
});

describe("StudySessionPage", () => {
  it("offers maintainers an edit link that opens in a new tab", async () => {
    scriptedApi({ roles: ["maintainer"] });
    renderSession();
    const link = await screen.findByRole("link", { name: "Wort bearbeiten (neuer Tab)" });
    expect(link).toHaveAttribute("href", expect.stringMatching(/^\/edit\/vocab\/\d+$/));
    expect(link).toHaveAttribute("target", "_blank");
  });

  it("hides the edit link without a write role", async () => {
    scriptedApi({ roles: [] });
    renderSession();
    await screen.findByText("Neues Wort");
    expect(screen.queryByRole("link", { name: /Wort bearbeiten/ })).not.toBeInTheDocument();
  });

  it("runs introduction, multiple choice with feedback, then the summary", async () => {
    const fetchMock = scriptedApi();
    const user = userEvent.setup();
    renderSession();

    // Introduction: the word, its meanings and the sample verse.
    expect(await screen.findByText("Neues Wort")).toBeInTheDocument();
    expect(screen.getByText("Rede")).toBeInTheDocument();
    expect(screen.getByText("ἐκ + πορεύω")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: /1,1/ })).toHaveAttribute("href", "/bible/43/1#v1");
    await user.click(screen.getByRole("button", { name: "Verstanden" }));

    // Multiple choice: a wrong pick shows the correct answers and the strength.
    expect(await screen.findByText("Was bedeutet …")).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: /Gesetz/ }));
    expect(await screen.findByText("✗ Leider nicht")).toBeInTheDocument();
    expect(screen.getByText("Wort, Rede")).toBeInTheDocument();
    expect(screen.getByText("Kommt in dieser Sitzung wieder")).toBeInTheDocument();

    // Enter continues to the summary.
    await user.keyboard("{Enter}");
    expect(await screen.findByText("Gut gemacht!")).toBeInTheDocument();
    expect(screen.getByText("Neu gelernt")).toBeInTheDocument();
    // Coverage grew from 61.1 % (when the page opened) to 62.4 %.
    expect(screen.getByText("62,4 %")).toBeInTheDocument();
    expect(screen.getByText("+1,3 %")).toBeInTheDocument();

    const bodies = fetchMock.mock.calls
      .filter(([url]) => String(url).endsWith("/answers"))
      .map(([, init]) => JSON.parse(String(init?.body)) as unknown);
    expect(bodies).toHaveLength(2);
    expect(bodies[1]).toMatchObject({ seq: 2, optionId: 0 });
  });

  it("picks an option with the number keys", async () => {
    scriptedApi();
    const user = userEvent.setup();
    renderSession();

    expect(await screen.findByText("Neues Wort")).toBeInTheDocument();
    await user.keyboard("{Enter}");
    expect(await screen.findByText("Was bedeutet …")).toBeInTheDocument();
    await user.keyboard("2");
    expect(await screen.findByText("✓ Richtig")).toBeInTheDocument();
    expect(screen.getByText("Stärker geworden")).toBeInTheDocument();
  });

  it("shows the Geändert notice at the start without blocking the session", async () => {
    scriptedApi({ changedCount: 2 });
    renderSession();
    expect(await screen.findByText("2 deiner Wörter wurden überarbeitet.")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Ansehen" })).toBeInTheDocument();
    expect(await screen.findByText("Neues Wort")).toBeInTheDocument();
  });

  it("says when nothing is due", async () => {
    scriptedApi({ nothingDue: true });
    renderSession();
    expect(await screen.findByText("Nichts zu lernen")).toBeInTheDocument();
    expect(screen.queryByText("Neues Wort")).toBeNull();
  });
});

const reverseCard = {
  seq: 2,
  kind: "reverse_multiple_choice",
  prompt: "Wort, Rede",
  // The question carries no lemma data (roadmap 0022.9): the entry is blank until the answer.
  entry: { id: 56, lemma: "", lang: "grc" },
  options: [
    { id: 0, text: "νόμος", lang: "grc" },
    { id: 1, text: "λόγος", lang: "grc" },
    { id: 2, text: "ῥῆμα", lang: "grc" },
    { id: 3, text: "λαός", lang: "grc" },
  ],
};

/** A session that starts straight with a reverse multiple-choice card and ends after its answer. */
function reverseApi(options: { noneCorrect?: boolean } = {}) {
  let answered = false;
  const card = {
    ...reverseCard,
    options: [...reverseCard.options, { id: 4, text: "None of the above", none: true }].filter(
      (option) => !options.noneCorrect || option.text !== "λόγος",
    ),
  };
  const fetchMock = vi.fn(async (url: string, init?: RequestInit) => {
    const path = url.replace(/^\/api/, "");
    const method = init?.method ?? "GET";
    if (path === "/auth/me") return json(me);
    if (path === "/meta-bible/structure") return json(fixtureStructure);
    if (method === "POST" && path === "/vocab/sessions") {
      return json({ ...started, newWords: 0, reviewWords: 1 });
    }
    if (method === "GET" && path === "/vocab/courses") return json({ courses: [] });
    if (method === "GET" && path === "/vocab/sessions/7/next") {
      if (!answered) {
        return json({ sessionId: 7, finished: false, progress: progress(0, 1), card });
      }
      return json({ sessionId: 7, finished: true, progress: progress(1, 2), summary });
    }
    if (method === "POST" && path === "/vocab/sessions/7/answers") {
      const body = JSON.parse(String(init?.body)) as { optionId?: number };
      answered = true;
      const rightId = options.noneCorrect ? 4 : 1;
      const right = body.optionId === rightId;
      return json({
        seq: 2,
        kind: "reverse_multiple_choice",
        correct: right,
        correctOptionId: rightId,
        correctAnswers: ["λόγος"],
        given: ["νόμος", "λόγος", "ῥῆμα", "λαός", "None of the above"][body.optionId ?? 0],
        entry,
        strength: right
          ? { before: 2, after: 3, change: "moved" }
          : { before: 2, after: 1, change: "dropped" },
        finished: true,
        progress: progress(1, 2),
        summary,
      });
    }
    return new Response("", { status: 404 });
  });
  vi.stubGlobal("fetch", fetchMock);
  return fetchMock;
}

describe("StudySessionPage reverse multiple choice", () => {
  it("asks for the lemma by its German meaning, with Greek options and no lemma on the question", async () => {
    reverseApi();
    renderSession();

    expect(await screen.findByText("Welches Wort heißt …")).toBeInTheDocument();
    expect(screen.getByText("Wort, Rede")).toBeInTheDocument();
    // The question shows no dictionary form or lemma: only the options are Greek.
    expect(screen.queryByText("λόγος, -ου, ὁ")).toBeNull();
    expect(screen.getByRole("button", { name: /λόγος/ })).toHaveAttribute("lang", "grc");
    expect(screen.getByRole("button", { name: /νόμος/ })).toHaveAttribute("lang", "grc");
  });

  it("gives feedback with the lemma as the correct answer after a wrong pick", async () => {
    const fetchMock = reverseApi();
    const user = userEvent.setup();
    renderSession();

    await user.click(await screen.findByRole("button", { name: /νόμος/ }));
    expect(await screen.findByText("✗ Leider nicht")).toBeInTheDocument();
    // The correct lemma is named in the feedback line (the verse below also shows the word).
    expect(screen.getByText(/^Richtig:/)).toHaveTextContent("Richtig: λόγος");
    expect(screen.getByText("Kommt in dieser Sitzung wieder")).toBeInTheDocument();

    const answer = fetchMock.mock.calls.find(([url]) => String(url).endsWith("/answers"));
    expect(JSON.parse(String(answer?.[1]?.body))).toMatchObject({ seq: 2, optionId: 0 });
  });

  it("shows the localised fifth option, set in plain text, and reaches it with key 5", async () => {
    const fetchMock = reverseApi();
    const user = userEvent.setup();
    renderSession();

    const none = await screen.findByRole("button", { name: /Keine der Antworten/ });
    expect(none).not.toHaveAttribute("lang", "grc");
    await user.keyboard("5");
    expect(await screen.findByText("✗ Leider nicht")).toBeInTheDocument();
    const answer = fetchMock.mock.calls.find(([url]) => String(url).endsWith("/answers"));
    expect(JSON.parse(String(answer?.[1]?.body))).toMatchObject({ optionId: 4 });
  });

  it("names the real answer when none of the above was the right one", async () => {
    reverseApi({ noneCorrect: true });
    const user = userEvent.setup();
    renderSession();

    await user.click(await screen.findByRole("button", { name: /Keine der Antworten/ }));
    expect(await screen.findByText("✓ Richtig")).toBeInTheDocument();
    expect(screen.getByText(/Keine der vier Antworten stimmte/)).toBeInTheDocument();
    expect(screen.getByText(/^Richtig:/)).toHaveTextContent("Richtig: λόγος");
  });

  it("accepts the number keys and marks the right lemma", async () => {
    reverseApi();
    const user = userEvent.setup();
    renderSession();

    expect(await screen.findByText("Welches Wort heißt …")).toBeInTheDocument();
    await user.keyboard("2");
    expect(await screen.findByText("✓ Richtig")).toBeInTheDocument();
    expect(screen.getByText("Stärker geworden")).toBeInTheDocument();
  });
});

const typingCard = {
  seq: 3,
  kind: "typing",
  typingPrompt: ["Mitte", "mitten"],
  entry: { id: 56, lemma: "", lang: "grc", pos: "Adjektiv" },
  layout: [
    { kind: "group", length: 5 },
    { kind: "separator", text: ", -" },
    { kind: "group", length: 1 },
    { kind: "separator", text: ", -" },
    { kind: "group", length: 2 },
  ],
};

/** A session with one typing card; the second group is judged wrong. */
function typingApi() {
  let answered = false;
  const fetchMock = vi.fn(async (url: string, init?: RequestInit) => {
    const path = url.replace(/^\/api/, "");
    const method = init?.method ?? "GET";
    if (path === "/auth/me") return json(me);
    if (path === "/meta-bible/structure") return json(fixtureStructure);
    if (method === "POST" && path === "/vocab/sessions") {
      return json({ ...started, newWords: 0, reviewWords: 1 });
    }
    if (method === "GET" && path === "/vocab/courses") return json({ courses: [] });
    if (method === "GET" && path === "/vocab/sessions/7/next") {
      if (!answered) {
        return json({ sessionId: 7, finished: false, progress: progress(0, 1), card: typingCard });
      }
      return json({ sessionId: 7, finished: true, progress: progress(1, 2), summary });
    }
    if (method === "POST" && path === "/vocab/sessions/7/answers") {
      answered = true;
      return json({
        seq: 3,
        kind: "typing",
        correct: false,
        correctAnswers: ["μέσος, -η, -ον"],
        groupResults: [true, false, true],
        given: "μέσος, -ο, -ον",
        entry,
        strength: { before: 2, after: 1, change: "dropped" },
        finished: true,
        progress: progress(1, 2),
        summary,
      });
    }
    return new Response("", { status: 404 });
  });
  vi.stubGlobal("fetch", fetchMock);
  return fetchMock;
}

describe("StudySessionPage typing", () => {
  it("shows the meanings and the boxes with the separators", async () => {
    typingApi();
    renderSession();

    expect(await screen.findByText("Tippe die Lernform")).toBeInTheDocument();
    expect(screen.getByText("Mitte, mitten")).toBeInTheDocument();
    expect(screen.getAllByLabelText("Buchstabe")).toHaveLength(8);
    expect(screen.getByRole("button", { name: "Prüfen" })).toBeDisabled();
  });

  it("posts one answer per group on Enter and shows the result per group", async () => {
    const fetchMock = typingApi();
    const user = userEvent.setup();
    renderSession();

    await screen.findByText("Tippe die Lernform");
    // The number keys type into the boxes (no letters, ignored) and never pick anything.
    await user.keyboard("1");
    await user.keyboard("mesoj");
    await user.click(screen.getByRole("button", { name: "ο" }));
    await user.keyboard("{Enter}");
    // Only 6 of 8 boxes are filled: nothing is sent.
    expect(fetchMock.mock.calls.some(([url]) => String(url).endsWith("/answers"))).toBe(false);
    await user.keyboard("on");
    await user.keyboard("{Enter}");

    expect(await screen.findByText("✗ Leider nicht")).toBeInTheDocument();
    const request = fetchMock.mock.calls.find(([url]) => String(url).endsWith("/answers"));
    const body = JSON.parse(String(request?.[1]?.body));
    expect(body).toMatchObject({ seq: 3, answers: ["μεσος", "ο", "ον"] });
    expect(body.optionId).toBeUndefined();

    const boxes = screen.getAllByLabelText("Buchstabe");
    expect(boxes[0]).toHaveClass("border-sev-ok");
    expect(boxes[5]).toHaveClass("border-sev-error");
    expect(boxes[6]).toHaveClass("border-sev-ok");
    expect(screen.getByText(/^Richtig:/)).toHaveTextContent("Richtig: μέσος, -η, -ον");
  });

  it("types with the on-screen keyboard and submits with the check button", async () => {
    const fetchMock = typingApi();
    const user = userEvent.setup();
    renderSession();

    await screen.findByText("Tippe die Lernform");
    for (const letter of "μεσοςηον") {
      await user.click(screen.getByRole("button", { name: letter }));
    }
    await user.click(screen.getByRole("button", { name: "Löschen" }));
    await user.click(screen.getByRole("button", { name: "ν" }));
    await user.click(screen.getByRole("button", { name: "Prüfen" }));

    expect(await screen.findByText("✗ Leider nicht")).toBeInTheDocument();
    const request = fetchMock.mock.calls.find(([url]) => String(url).endsWith("/answers"));
    expect(JSON.parse(String(request?.[1]?.body))).toMatchObject({ answers: ["μεσος", "η", "ον"] });
  });
});

// The listening card names no lemma: the options are the only Greek on the question.
const listeningCard = {
  seq: 4,
  kind: "listening",
  entry: { id: 56, lang: "grc", audioUrl: "https://audio.example/logos.mp3" },
  options: [
    { id: 0, text: "λαός", lang: "grc" },
    { id: 1, text: "λόγος", lang: "grc" },
    { id: 2, text: "λίθος", lang: "grc" },
    { id: 3, text: "λύω", lang: "grc" },
  ],
};

const listeningAnswer = {
  seq: 4,
  kind: "listening",
  correct: false,
  correctOptionId: 1,
  correctAnswers: ["λόγος"],
  given: "λαός",
  entry,
  strength: { before: 2, after: 1, change: "dropped" },
  finished: true,
  progress: progress(1, 2),
  summary,
};

const verseContextCard = {
  seq: 5,
  kind: "verse_context",
  entry,
  options: [
    { id: 0, text: "Licht" },
    { id: 1, text: "Wort" },
    { id: 2, text: "Leben" },
    { id: 3, text: "Geist" },
  ],
  // The German hit word is a gap: empty text and `isGap`.
  verse: {
    bibleBookId: 43,
    chapter: 1,
    verse: 1,
    fromRevision: true,
    humanMade: false,
    original: [
      { position: 1, text: "Ἐν", isHit: false },
      { position: 2, text: "ἀρχῇ", isHit: false },
      { position: 3, text: "ἦν", isHit: false },
      { position: 4, text: "ὁ", isHit: false },
      { position: 5, text: "λόγος", isHit: true },
    ],
    german: [
      { position: 1, text: "Im", isHit: false },
      { position: 2, text: "Anfang", isHit: false },
      { position: 3, text: "war", isHit: false },
      { position: 4, text: "das", isHit: false },
      { position: 5, text: "", isHit: true, isGap: true },
    ],
  },
};

// The answer carries the full verse: the German hit word is no gap any more.
const verseContextAnswer = {
  seq: 5,
  kind: "verse_context",
  correct: false,
  correctOptionId: 1,
  correctAnswers: ["Wort"],
  given: "Licht",
  entry,
  strength: { before: 2, after: 1, change: "dropped" },
  verse: {
    ...verseContextCard.verse,
    german: [
      { position: 1, text: "Im", isHit: false },
      { position: 2, text: "Anfang", isHit: false },
      { position: 3, text: "war", isHit: false },
      { position: 4, text: "das", isHit: false },
      { position: 5, text: "Wort", isHit: true },
    ],
  },
  finished: true,
  progress: progress(1, 2),
  summary,
};

/** A session with one card, answered with `answer`; the session ends after the answer. */
function singleCardApi(card: object, answer: object) {
  let answered = false;
  const fetchMock = vi.fn(async (url: string, init?: RequestInit) => {
    const path = url.replace(/^\/api/, "");
    const method = init?.method ?? "GET";
    if (path === "/auth/me") return json(me);
    if (path === "/meta-bible/structure") return json(fixtureStructure);
    if (method === "POST" && path === "/vocab/sessions") {
      return json({ ...started, newWords: 0, reviewWords: 1 });
    }
    if (method === "GET" && path === "/vocab/courses") return json({ courses: [] });
    if (method === "GET" && path === "/vocab/sessions/7/next") {
      if (!answered) {
        return json({ sessionId: 7, finished: false, progress: progress(0, 1), card });
      }
      return json({ sessionId: 7, finished: true, progress: progress(1, 2), summary });
    }
    if (method === "POST" && path === "/vocab/sessions/7/answers") {
      answered = true;
      return json(answer);
    }
    return new Response("", { status: 404 });
  });
  vi.stubGlobal("fetch", fetchMock);
  return fetchMock;
}

describe("StudySessionPage listening and verse context", () => {
  it("plays the audio on click and shows no lemma before the answer", async () => {
    const plays: string[] = [];
    class FakeAudio {
      play = vi.fn(() => Promise.resolve());
      pause = vi.fn();
      constructor(url: string) {
        plays.push(url);
      }
    }
    vi.stubGlobal("Audio", FakeAudio);
    singleCardApi(listeningCard, listeningAnswer);
    const user = userEvent.setup();
    renderSession();

    expect(await screen.findByText("Hören")).toBeInTheDocument();
    expect(document.querySelector('p[lang="grc"]')).toBeNull();
    expect(screen.queryByText("λόγος, -ου, ὁ")).toBeNull();
    await user.click(screen.getByRole("button", { name: "Abspielen" }));
    expect(plays).toEqual(["https://audio.example/logos.mp3"]);

    await user.click(screen.getByRole("button", { name: /λαός/ }));
    expect(await screen.findByText("✗ Leider nicht")).toBeInTheDocument();
    expect(screen.getByText(/^Richtig:/)).toHaveTextContent("Richtig: λόγος");
  });

  it("renders the gap without the German word, then the full verse with the reader link", async () => {
    singleCardApi(verseContextCard, verseContextAnswer);
    const user = userEvent.setup();
    renderSession();

    expect(await screen.findByText("Im Vers")).toBeInTheDocument();
    expect(screen.getByText("λόγος", { selector: "mark" })).toBeInTheDocument();
    expect(screen.getByText("Im Anfang war das")).not.toHaveTextContent("Wort");
    expect(screen.getByRole("img", { name: "Lücke" })).toBeInTheDocument();
    // No reader link before the answer.
    expect(screen.queryByRole("link", { name: /1,1/ })).toBeNull();

    await user.click(screen.getByRole("button", { name: /Licht/ }));
    expect(await screen.findByText("✗ Leider nicht")).toBeInTheDocument();
    expect(screen.getByText(/^Richtig:/)).toHaveTextContent("Richtig: Wort");
    expect(screen.getByText("Wort", { selector: "mark" }).parentElement).toHaveTextContent(
      "Im Anfang war das Wort",
    );
    expect(screen.queryByRole("img", { name: "Lücke" })).toBeNull();
    expect(screen.getByRole("link", { name: /1,1/ })).toHaveAttribute("href", "/bible/43/1#v1");
  });
});
