import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { type ReactNode, useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { Link, Navigate, useParams } from "react-router";
import { AudioField } from "@/components/AudioField";
import { Button, buttonClasses } from "@/components/Button";
import { ChoiceList } from "@/components/ChoiceList";
import { EmptyState } from "@/components/EmptyState";
import { FormError } from "@/components/FormError";
import { LetterBoxInput } from "@/components/LetterBoxInput";
import { ProgressBar } from "@/components/ProgressBar";
import { ScriptKeyboard } from "@/components/ScriptKeyboard";
import { StatTile } from "@/components/StatTile";
import { StrengthBar } from "@/components/StrengthBar";
import { StudyCard } from "@/components/StudyCard";
import { VerseContext } from "@/components/VerseContext";
import { ApiError } from "@/lib/api";
import { useAuth } from "@/lib/auth/AuthContext";
import { hasWriteRole } from "@/lib/auth/roles";
import { useBibleStructure } from "@/lib/bible/queries";
import { getCourses, type VocabLang, type VocabSampleVerse } from "@/lib/vocab/api";
import { formatPos } from "@/lib/vocab/pos";
import { BACKSPACE_KEY } from "@/lib/vocab/scriptLayouts";
import {
  getVocabSessionNext,
  type SubmitVocabAnswerRequest,
  startVocabSession,
  submitVocabAnswer,
  type VocabAnswerResponse,
  type VocabCard,
  type VocabSessionProgress,
  type VocabSessionSummary,
} from "@/lib/vocab/sessionApi";
import {
  emptyAnswers,
  isComplete,
  popLetter,
  pushLetter,
  toTypingLayout,
} from "@/lib/vocab/typing";
import { useAudioPlayer } from "@/lib/vocab/useAudioPlayer";
import { sampleVerseHref, sampleVerseReference } from "@/lib/vocab/verseReference";
import { ChangedWordsNotice } from "@/pages/vocab/ChangedWordsNotice";

/** A multiple-choice card after its answer: the card as shown and the server's verdict. */
interface Feedback {
  card: VocabCard;
  result: VocabAnswerResponse;
}

/** Coverage of the course in the summary: the value now and how much it grew (null when it did not). */
interface CoverageGrowth {
  title: string;
  value: number;
  delta: number | null;
}

/** Cards with options: the multiple choice kinds (roadmap 0022.9) and listening and verse context (0022.11). */
function isChoiceKind(kind: VocabCard["kind"]): boolean {
  return (
    kind === "multiple_choice" ||
    kind === "reverse_multiple_choice" ||
    kind === "listening" ||
    kind === "verse_context"
  );
}

/** The growth in percentage points, one decimal; null when the coverage did not grow. */
function pointsGrown(before: number, after: number): number | null {
  const delta = Math.round((after - before) * 10) / 10;
  return delta > 0 ? delta : null;
}

/**
 * `/learn/:course` (roadmap 0022.8f, 0022.9f): the study session, one card per screen. Introductions,
 * multiple choice, reverse multiple choice, listening and verse context for now. Keys 1–5 pick an option, Enter continues.
 */
export function StudySessionPage() {
  const { status } = useAuth();
  if (status === "loading") return null;
  if (status === "anonymous") return <Navigate to="/login" replace />;
  return <StudySession />;
}

/** The answer and the next card of one step report the same `cardsServed`; the one with more steps done is newer. */
function isNewer(candidate: VocabSessionProgress, current: VocabSessionProgress | undefined) {
  if (!current) return true;
  if (candidate.cardsServed !== current.cardsServed)
    return candidate.cardsServed > current.cardsServed;
  return candidate.stepsDone >= current.stepsDone;
}

function StudySession() {
  const { user } = useAuth();
  const { t, i18n } = useTranslation();
  const { course = "" } = useParams();
  const queryClient = useQueryClient();
  const structure = useBibleStructure();
  // Bumped by "Noch eine Sitzung": a new start request, so the session starts again.
  const [attempt, setAttempt] = useState(0);
  const [picked, setPicked] = useState<number | null>(null);
  const [feedback, setFeedback] = useState<Feedback | null>(null);
  // The card last shown and when it appeared; the response time is measured from there.
  const shown = useRef({ seq: 0, at: performance.now() });
  const primaryRef = useRef<HTMLButtonElement>(null);
  const audio = useAudioPlayer();
  const stopAudio = audio.stop;
  const number = (value: number) => value.toLocaleString(i18n.language);

  // POST resumes the open session of the course or starts one; it is only asked once per attempt.
  const start = useQuery({
    queryKey: ["vocab", "session", course, attempt],
    queryFn: () => startVocabSession(course),
    retry: false,
    staleTime: Number.POSITIVE_INFINITY,
    gcTime: 0,
    refetchOnWindowFocus: false,
  });
  const sessionId = start.data && start.data.sessionId > 0 ? start.data.sessionId : 0;
  const nextKey = ["vocab", "session", sessionId, "next"] as const;
  const next = useQuery({
    queryKey: nextKey,
    queryFn: ({ signal }) => getVocabSessionNext(sessionId, signal),
    enabled: sessionId > 0,
    retry: false,
    gcTime: 0,
    refetchOnWindowFocus: false,
  });
  const answer = useMutation({
    mutationFn: (request: SubmitVocabAnswerRequest) => submitVocabAnswer(sessionId, request),
  });

  // Clears the cached card so the page shows the loading state until the server's next card arrives.
  const resetNext = () => queryClient.resetQueries({ queryKey: nextKey, exact: true });

  const card = feedback?.card ?? next.data?.card ?? null;
  const cardSeq = card?.seq;
  const fresh: VocabSessionProgress | undefined =
    feedback?.result.progress ?? next.data?.progress ?? start.data?.progress;
  // While the next card loads (after "Weiter") `fresh` falls back to an older value; the bar keeps the last
  // value it showed until a response at least as new arrives.
  const [shownProgress, setShownProgress] = useState<VocabSessionProgress | undefined>(undefined);
  if (fresh && fresh !== shownProgress && isNewer(fresh, shownProgress)) setShownProgress(fresh);
  const progress = shownProgress ?? fresh;
  const nothingDue = start.data?.finished === true && start.data.sessionId === 0;
  // Flagged words of the learner: the notice shows them at the start and never blocks the session.
  const changedCount = start.data?.changedCount ?? 0;

  // Coverage of the course (roadmap 0022.12f): the value fetched when the page opens is the one before
  // any answer; the summary refetches the courses and compares.
  const courses = useQuery({
    queryKey: ["vocab", "courses"],
    queryFn: ({ signal }) => getCourses(signal),
    refetchOnMount: "always",
  });
  const courseEntry = courses.data?.courses.find((item) => item.code === course);
  const coverageNow = courseEntry?.coverage ?? null;
  const [coverageBefore, setCoverageBefore] = useState<number | null>(null);
  useEffect(() => {
    if (coverageBefore === null && courses.isFetchedAfterMount && coverageNow !== null) {
      setCoverageBefore(coverageNow);
    }
  }, [coverageBefore, courses.isFetchedAfterMount, coverageNow]);
  const summary = next.data?.finished && feedback === null ? (next.data.summary ?? null) : null;
  const summaryShown = summary !== null;
  const refetchCourses = courses.refetch;
  useEffect(() => {
    if (summaryShown) void refetchCourses();
  }, [summaryShown, refetchCourses]);
  const growth: CoverageGrowth | null =
    courseEntry && coverageNow !== null
      ? {
          title: t(`learn.summary.coverageTitle.${courseEntry.track}`),
          value: coverageNow,
          delta: coverageBefore === null ? null : pointsGrown(coverageBefore, coverageNow),
        }
      : null;

  // A new card stops the audio of the card before.
  useEffect(() => {
    shown.current = { seq: cardSeq ?? 0, at: performance.now() };
    stopAudio();
  }, [cardSeq, stopAudio]);

  // The primary button of an introduction or of the feedback takes focus, so Enter continues.
  const focusKey = feedback ? "feedback" : card?.kind === "introduction" ? `intro-${card.seq}` : "";
  useEffect(() => {
    if (focusKey) primaryRef.current?.focus();
  }, [focusKey]);

  function answerCard(current: VocabCard, optionId?: number, answers?: string[]) {
    const responseMs = Math.round(performance.now() - shown.current.at);
    const request: SubmitVocabAnswerRequest = answers
      ? { seq: current.seq, answers, responseMs }
      : optionId === undefined
        ? { seq: current.seq, responseMs }
        : { seq: current.seq, optionId, responseMs };
    answer.mutate(request, {
      onSuccess: (result) => {
        // An introduction has no verdict: the next card follows directly.
        if (current.kind === "introduction") {
          void resetNext();
        } else {
          setFeedback({ card: current, result });
        }
      },
      onError: (error) => {
        setPicked(null);
        // 409: the card was already answered elsewhere; fetch the card the server has now.
        if (error instanceof ApiError && error.status === 409) void resetNext();
      },
    });
  }

  function pick(current: VocabCard, index: number) {
    const option = current.options?.[index];
    if (!option || picked !== null || answer.isPending) return;
    setPicked(index);
    answerCard(current, option.id);
  }

  function continueFromFeedback() {
    setFeedback(null);
    setPicked(null);
    void resetNext();
  }

  function startAgain() {
    setFeedback(null);
    setPicked(null);
    setAttempt((value) => value + 1);
  }

  // Keys go through a ref so the listener always sees the current card and answer state.
  const handleKey = (event: KeyboardEvent) => {
    if (event.defaultPrevented || event.altKey || event.ctrlKey || event.metaKey) return;
    const target = event.target instanceof HTMLElement ? event.target : null;
    if (target && ["INPUT", "TEXTAREA", "SELECT"].includes(target.tagName)) return;
    if (event.key === "Enter") {
      // A focused button or link acts on Enter by itself.
      if (target && ["BUTTON", "A"].includes(target.tagName)) return;
      if (feedback) continueFromFeedback();
      else if (card?.kind === "introduction" && !answer.isPending) answerCard(card);
      return;
    }
    if (card && isChoiceKind(card.kind) && !feedback && /^[1-5]$/.test(event.key)) {
      pick(card, Number(event.key) - 1);
    }
  };
  const keyHandler = useRef(handleKey);
  useEffect(() => {
    keyHandler.current = handleKey;
  });
  useEffect(() => {
    const listener = (event: KeyboardEvent) => keyHandler.current(event);
    window.addEventListener("keydown", listener);
    return () => window.removeEventListener("keydown", listener);
  }, []);

  const failure = (error: unknown) =>
    error instanceof ApiError && error.status === 401
      ? t("learn.session.signIn")
      : t("learn.session.loadFailed");
  const answerFailed =
    answer.isError && !(answer.error instanceof ApiError && answer.error.status === 409);

  // The reader link only shows once the card is answered (a verse-context question must not lead to its answer).
  const renderVerse = (verse: VocabSampleVerse, lang: VocabLang, link = true) => (
    <VerseContext
      reference={sampleVerseReference(verse, structure.data, i18n.language)}
      href={link ? sampleVerseHref(verse) : undefined}
      lang={lang}
      original={verse.original}
      german={verse.german}
    />
  );

  const introductionView = (current: VocabCard) => {
    const { entry } = current;
    const dir = entry.lang === "he" ? "rtl" : undefined;
    const meta = [entry.transliteration, formatPos(entry.pos, t)].filter(Boolean).join(" · ");
    return (
      <StudyCard kindLabel={t("learn.kind.introduction")}>
        <div className="grid justify-items-center gap-2 text-center">
          <p lang={entry.lang} dir={dir} className="font-original text-5xl">
            {entry.lemma}
          </p>
          {entry.dictionaryForm ? (
            <p lang={entry.lang} dir={dir} className="font-original text-2xl text-fg-muted">
              {entry.dictionaryForm}
            </p>
          ) : null}
          {meta ? <p className="text-sm text-fg-muted">{meta}</p> : null}
          <AudioField src={entry.audioUrl ?? null} readOnly />
        </div>
        {current.meanings?.length ? (
          <ol className="grid gap-1 text-sm">
            {current.meanings.map((meaning) => (
              <li key={meaning.rank} className="flex justify-between gap-2">
                <span className="font-medium">{meaning.german}</span>
                {meaning.count != null ? (
                  <span className="tabular-nums text-fg-muted">{number(meaning.count)}×</span>
                ) : null}
              </li>
            ))}
          </ol>
        ) : null}
        {renderRoot(current)}
        {current.verse ? renderVerse(current.verse, entry.lang) : null}
        <Button
          ref={primaryRef}
          variant="primary"
          size="md"
          disabled={answer.isPending}
          onClick={() => answerCard(current)}
        >
          {t("learn.intro.continue")}
        </Button>
      </StudyCard>
    );
  };

  const choiceView = (current: VocabCard) => {
    const { entry } = current;
    const dir = entry.lang === "he" ? "rtl" : undefined;
    const options = current.options ?? [];
    // A reverse card asks for the lemma, so its question is the German prompt and its options are lemmas.
    const reverse = current.kind === "reverse_multiple_choice";
    const listening = current.kind === "listening";
    const verseContext = current.kind === "verse_context";
    const optionLang = options.find((option) => option.lang)?.lang ?? "de";
    const noneIndex = options.findIndex((option) => option.none);
    const correctIndex = feedback
      ? options.findIndex((option) => option.id === feedback.result.correctOptionId)
      : -1;
    // "None of the above" was the right answer: the feedback must still say what the word means.
    const noneWasRight = feedback !== null && correctIndex >= 0 && correctIndex === noneIndex;
    const kindKey = listening
      ? "learn.kind.listening"
      : verseContext
        ? "learn.kind.verseContext"
        : reverse
          ? "learn.kind.reverseMultipleChoice"
          : "learn.kind.multipleChoice";
    // No lemma before the answer: a listening card shows only the audio, a verse card the verse with its gap.
    let question: ReactNode;
    if (listening) {
      question = (
        <div className="grid justify-items-center gap-2">
          <Button
            variant="primary"
            size="md"
            aria-label={t("learn.listening.play")}
            className="h-20 w-20 rounded-full text-3xl"
            disabled={!entry.audioUrl}
            onClick={() => {
              if (entry.audioUrl) audio.play(entry.audioUrl);
            }}
          >
            ▶
          </Button>
          {audio.failed ? (
            <p role="alert" className="text-sm">
              {t("learn.listening.failed")}
            </p>
          ) : null}
        </div>
      );
    } else if (verseContext) {
      question = (
        <>
          {current.verse && !feedback ? renderVerse(current.verse, entry.lang, false) : null}
          <p className="text-sm text-fg-muted">{t("learn.verse.question")}</p>
        </>
      );
    } else if (reverse) {
      question = <p className="text-center text-3xl font-semibold">{current.prompt ?? ""}</p>;
    } else {
      question = (
        <div className="grid justify-items-center gap-1 text-center">
          <p lang={entry.lang} dir={dir} className="font-original text-5xl">
            {entry.lemma}
          </p>
          {entry.dictionaryForm ? (
            <p lang={entry.lang} dir={dir} className="font-original text-xl text-fg-muted">
              {entry.dictionaryForm}
            </p>
          ) : null}
        </div>
      );
    }
    return (
      <StudyCard kindLabel={t(kindKey)}>
        {question}
        <ChoiceList
          options={options.map((option) =>
            option.none ? t("learn.session.noneOfTheAbove") : option.text,
          )}
          lang={optionLang}
          plainIndex={noneIndex >= 0 ? noneIndex : null}
          state={{ picked, correct: correctIndex >= 0 ? correctIndex : null }}
          onPick={(index) => pick(current, index)}
        />
        {feedback ? renderFeedback(feedback.result, noneWasRight) : null}
      </StudyCard>
    );
  };

  /** The root line on the back of a card: `prefixes + root`, nothing for a simple word. */
  const renderRoot = (shown: VocabCard) => {
    const root = shown.root;
    if (!root) return null;
    const parts = [root.prefixes, root.root].filter(Boolean).join(" + ");
    return (
      <p lang={shown.entry.lang} className="font-original text-fg-muted">
        {!root.prefixes && root.strongsKey ? `${parts} (${root.strongsKey})` : parts}
      </p>
    );
  };

  const renderFeedback = (result: VocabAnswerResponse, noneWasRight = false) => (
    <div role="status" className="grid gap-3 border-t border-border pt-4">
      <p className="text-lg font-semibold">
        {result.correct ? t("learn.feedback.correct") : t("learn.feedback.wrong")}
      </p>
      {noneWasRight ? <p className="text-sm">{t("learn.feedback.noneWasRight")}</p> : null}
      {result.correct && !noneWasRight ? null : (
        <p className="text-sm">
          {t("learn.feedback.correctAnswers")}:{" "}
          <span
            lang={
              ["typing", "listening", "reverse_multiple_choice"].includes(result.kind)
                ? result.entry.lang
                : undefined
            }
            className="font-medium"
          >
            {result.correctAnswers.join(", ")}
          </span>
        </p>
      )}
      {feedback ? renderRoot(feedback.card) : null}
      {result.strength ? (
        <StrengthBar before={result.strength.before} after={result.strength.after} />
      ) : null}
      {result.verse ? renderVerse(result.verse, result.entry.lang) : null}
      <Button ref={primaryRef} variant="primary" size="md" onClick={continueFromFeedback}>
        {t("learn.feedback.next")}{" "}
        <span className="text-sm opacity-75">{t("learn.feedback.enter")}</span>
      </Button>
    </div>
  );

  const typingView = (current: VocabCard) => (
    <TypingCard
      key={current.seq}
      card={current}
      result={feedback?.card.seq === current.seq ? feedback.result : null}
      pending={answer.isPending}
      onSubmit={(answers) => answerCard(current, undefined, answers)}
      feedbackView={renderFeedback}
    />
  );

  let body: ReactNode;
  if (start.isError || next.isError) {
    body = <FormError message={failure(start.error ?? next.error)} />;
  } else if (nothingDue) {
    body = <NothingDue />;
  } else if (next.data?.finished && feedback === null) {
    body = summary ? (
      <SessionSummary summary={summary} coverage={growth} onAgain={startAgain} />
    ) : (
      <NothingDue />
    );
  } else if (card) {
    body = (
      <>
        {answerFailed ? <FormError message={t("learn.session.answerFailed")} /> : null}
        {card.kind === "introduction" ? (
          introductionView(card)
        ) : isChoiceKind(card.kind) ? (
          choiceView(card)
        ) : card.kind === "typing" && card.layout ? (
          typingView(card)
        ) : (
          <FormError message={t("learn.session.unsupported")} />
        )}
      </>
    );
  } else {
    body = (
      <p role="status" className="text-sm text-fg-muted">
        {t("learn.session.loading")}
      </p>
    );
  }

  return (
    <section className="mx-auto grid w-full max-w-2xl gap-4">
      <div className="flex items-center gap-3">
        <Link
          to="/learn"
          aria-label={t("learn.session.leave")}
          className={buttonClasses({ variant: "ghost", size: "sm" })}
        >
          ✕
        </Link>
        {progress ? (
          <div className="min-w-0 flex-1">
            <ProgressBar
              value={progress.stepsDone}
              max={progress.stepsTotal}
              label={t("learn.session.progress")}
            />
          </div>
        ) : (
          <div className="flex-1" />
        )}
        {card && hasWriteRole(user) ? (
          <a
            href={`/edit/vocab/${card.entry.id}`}
            target="_blank"
            rel="noopener noreferrer"
            className={buttonClasses({ variant: "ghost", size: "sm" })}
          >
            {t("learn.session.editWord")}
          </a>
        ) : null}
      </div>
      {changedCount > 0 ? <ChangedWordsNotice changedCount={changedCount} /> : null}
      {body}
    </section>
  );
}

interface TypingCardProps {
  card: VocabCard;
  /** The server's verdict once the card is answered. */
  result: VocabAnswerResponse | null;
  pending: boolean;
  onSubmit: (answers: string[]) => void;
  feedbackView: (result: VocabAnswerResponse) => ReactNode;
}

/** The typing card (roadmap 0022.10f): meanings on top, letter boxes per group and the script keyboard. */
function TypingCard({ card, result, pending, onSubmit, feedbackView }: TypingCardProps) {
  const { t } = useTranslation();
  const layout = toTypingLayout(card.layout ?? []);
  const [typed, setTyped] = useState(() => emptyAnswers(layout));
  const lang = card.entry.lang === "he" ? "he" : "grc";
  const complete = isComplete(layout, typed);
  const locked = result !== null || pending;
  const submit = () => {
    if (complete && !locked) onSubmit(typed);
  };
  const meta = [formatPos(card.entry.pos, t), t(`learn.typing.hint.${lang}`)]
    .filter(Boolean)
    .join(" · ");
  return (
    // biome-ignore lint/a11y/noStaticElementInteractions: Enter inside a letter box submits
    <div
      onKeyDown={(event) => {
        if (event.key === "Enter" && event.target instanceof HTMLInputElement) {
          event.preventDefault();
          submit();
        }
      }}
    >
      <StudyCard kindLabel={t("learn.kind.typing")}>
        <div className="grid justify-items-center gap-1 text-center">
          <p className="text-3xl font-semibold">{(card.typingPrompt ?? []).join(", ")}</p>
          <p className="text-sm text-fg-muted">{meta}</p>
        </div>
        <div className="grid justify-items-center">
          <LetterBoxInput
            layout={layout}
            value={typed}
            onChange={setTyped}
            dir={lang === "he" ? "rtl" : "ltr"}
            lang={lang}
            readOnly={locked}
            results={result?.groupResults ?? undefined}
          />
        </div>
        {result ? null : (
          <>
            <ScriptKeyboard
              layout={lang === "he" ? "hebrew" : "greek"}
              onKey={(key) =>
                setTyped((value) =>
                  key === BACKSPACE_KEY ? popLetter(layout, value) : pushLetter(layout, value, key),
                )
              }
            />
            <Button variant="primary" size="md" disabled={!complete || locked} onClick={submit}>
              {t("learn.typing.check")}
            </Button>
          </>
        )}
        {result ? feedbackView(result) : null}
      </StudyCard>
    </div>
  );
}

/** Nothing is due and there is no new word in the course. */
function NothingDue() {
  const { t } = useTranslation();
  return (
    <EmptyState title={t("learn.nothingDue.title")}>
      <span className="grid gap-3">
        <span>{t("learn.nothingDue.text")}</span>
        <Link to="/learn" className="text-sm text-accent underline">
          {t("learn.nothingDue.back")}
        </Link>
      </span>
    </EmptyState>
  );
}

/** The summary at the end of a session: what was learned and repeated, and the strength of each word. */
function SessionSummary({
  summary,
  coverage,
  onAgain,
}: {
  summary: VocabSessionSummary;
  coverage: CoverageGrowth | null;
  onAgain: () => void;
}) {
  const { t, i18n } = useTranslation();
  const number = (value: number) => value.toLocaleString(i18n.language);
  const percent = (value: number) =>
    value.toLocaleString(i18n.language, { minimumFractionDigits: 1, maximumFractionDigits: 1 });
  return (
    <StudyCard kindLabel={t("learn.summary.kind")}>
      <h2 className="text-2xl font-semibold">{t("learn.summary.title")}</h2>
      <div className={`grid gap-2 ${coverage ? "grid-cols-2 md:grid-cols-4" : "grid-cols-3"}`}>
        <StatTile title={t("learn.summary.newWords")} value={number(summary.newWords)} />
        <StatTile title={t("learn.summary.reviewedWords")} value={number(summary.reviewedWords)} />
        <StatTile
          title={t("learn.summary.correct")}
          value={`${number(summary.correctAnswers)} / ${number(summary.answers)}`}
        />
        {coverage ? (
          <StatTile
            title={coverage.title}
            value={t("learn.summary.coverageValue", { value: percent(coverage.value) })}
            delta={
              coverage.delta === null
                ? undefined
                : t("learn.summary.coverageDelta", { value: percent(coverage.delta) })
            }
          />
        ) : null}
      </div>
      <ul className="grid gap-2">
        {summary.words.map((word) => (
          <li key={word.entryId} className="flex flex-wrap items-center justify-between gap-2">
            <span
              lang={word.lang}
              dir={word.lang === "he" ? "rtl" : undefined}
              className="font-original text-xl"
            >
              {word.lemma}
            </span>
            <StrengthBar before={word.strengthBefore} after={word.strengthAfter} />
          </li>
        ))}
      </ul>
      <div className="flex flex-wrap gap-2">
        <Button variant="primary" size="md" onClick={onAgain}>
          {t("learn.summary.again")}
        </Button>
        <Link to="/learn" className={buttonClasses({ variant: "secondary", size: "md" })}>
          {t("learn.summary.overview")}
        </Link>
      </div>
    </StudyCard>
  );
}
