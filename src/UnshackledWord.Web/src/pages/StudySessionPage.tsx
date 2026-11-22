import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { type ReactNode, useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { Link, Navigate, useParams } from "react-router";
import { AudioField } from "@/components/AudioField";
import { Button, buttonClasses } from "@/components/Button";
import { ChoiceList } from "@/components/ChoiceList";
import { EmptyState } from "@/components/EmptyState";
import { FormError } from "@/components/FormError";
import { ProgressBar } from "@/components/ProgressBar";
import { StatTile } from "@/components/StatTile";
import { StrengthBar } from "@/components/StrengthBar";
import { StudyCard } from "@/components/StudyCard";
import { VerseContext } from "@/components/VerseContext";
import { ApiError } from "@/lib/api";
import { useAuth } from "@/lib/auth/AuthContext";
import { useBibleStructure } from "@/lib/bible/queries";
import type { VocabLang, VocabSampleVerse } from "@/lib/vocab/api";
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
import { sampleVerseHref, sampleVerseReference } from "@/lib/vocab/verseReference";

/** A multiple-choice card after its answer: the card as shown and the server's verdict. */
interface Feedback {
  card: VocabCard;
  result: VocabAnswerResponse;
}

/**
 * `/learn/:course` (roadmap 0022.8f): the study session, one card per screen. Introductions and
 * multiple choice only for now. Keys 1–4 pick an option, Enter continues.
 */
export function StudySessionPage() {
  const { status } = useAuth();
  if (status === "loading") return null;
  if (status === "anonymous") return <Navigate to="/login" replace />;
  return <StudySession />;
}

function StudySession() {
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
  const progress: VocabSessionProgress | undefined =
    feedback?.result.progress ?? next.data?.progress ?? start.data?.progress;
  const nothingDue = start.data?.finished === true && start.data.sessionId === 0;

  useEffect(() => {
    shown.current = { seq: cardSeq ?? 0, at: performance.now() };
  }, [cardSeq]);

  // The primary button of an introduction or of the feedback takes focus, so Enter continues.
  const focusKey = feedback ? "feedback" : card?.kind === "introduction" ? `intro-${card.seq}` : "";
  useEffect(() => {
    if (focusKey) primaryRef.current?.focus();
  }, [focusKey]);

  function answerCard(current: VocabCard, optionId?: number) {
    const responseMs = Math.round(performance.now() - shown.current.at);
    const request: SubmitVocabAnswerRequest =
      optionId === undefined
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
    if (card?.kind === "multiple_choice" && !feedback && /^[1-4]$/.test(event.key)) {
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

  const renderVerse = (verse: VocabSampleVerse, lang: VocabLang) => (
    <VerseContext
      reference={sampleVerseReference(verse, structure.data, i18n.language)}
      href={sampleVerseHref(verse)}
      lang={lang}
      original={verse.original}
      german={verse.german}
    />
  );

  const introductionView = (current: VocabCard) => {
    const { entry } = current;
    const dir = entry.lang === "he" ? "rtl" : undefined;
    const meta = [entry.transliteration, entry.pos].filter(Boolean).join(" · ");
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
    const correctIndex = feedback
      ? options.findIndex((option) => option.id === feedback.result.correctOptionId)
      : -1;
    return (
      <StudyCard kindLabel={t("learn.kind.multipleChoice")}>
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
        <ChoiceList
          options={options.map((option) => option.text)}
          state={{ picked, correct: correctIndex >= 0 ? correctIndex : null }}
          onPick={(index) => pick(current, index)}
        />
        {feedback ? (
          <div role="status" className="grid gap-3 border-t border-border pt-4">
            <p className="text-lg font-semibold">
              {feedback.result.correct ? t("learn.feedback.correct") : t("learn.feedback.wrong")}
            </p>
            {feedback.result.correct ? null : (
              <p className="text-sm">
                {t("learn.feedback.correctAnswers")}:{" "}
                <span className="font-medium">{feedback.result.correctAnswers.join(", ")}</span>
              </p>
            )}
            {feedback.result.strength ? (
              <StrengthBar
                before={feedback.result.strength.before}
                after={feedback.result.strength.after}
              />
            ) : null}
            {feedback.result.verse
              ? renderVerse(feedback.result.verse, feedback.result.entry.lang)
              : null}
            <Button ref={primaryRef} variant="primary" size="md" onClick={continueFromFeedback}>
              {t("learn.feedback.next")}{" "}
              <span className="text-sm opacity-75">{t("learn.feedback.enter")}</span>
            </Button>
          </div>
        ) : null}
      </StudyCard>
    );
  };

  let body: ReactNode;
  if (start.isError || next.isError) {
    body = <FormError message={failure(start.error ?? next.error)} />;
  } else if (nothingDue) {
    body = <NothingDue />;
  } else if (next.data?.finished && feedback === null) {
    body = next.data.summary ? (
      <SessionSummary summary={next.data.summary} onAgain={startAgain} />
    ) : (
      <NothingDue />
    );
  } else if (card) {
    body = (
      <>
        {answerFailed ? <FormError message={t("learn.session.answerFailed")} /> : null}
        {card.kind === "introduction" ? (
          introductionView(card)
        ) : card.kind === "multiple_choice" ? (
          choiceView(card)
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
              value={progress.wordsDone}
              max={progress.wordsTotal}
              label={t("learn.session.progress")}
            />
          </div>
        ) : null}
      </div>
      {body}
    </section>
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
  onAgain,
}: {
  summary: VocabSessionSummary;
  onAgain: () => void;
}) {
  const { t, i18n } = useTranslation();
  const number = (value: number) => value.toLocaleString(i18n.language);
  return (
    <StudyCard kindLabel={t("learn.summary.kind")}>
      <h2 className="text-2xl font-semibold">{t("learn.summary.title")}</h2>
      <div className="grid grid-cols-3 gap-2">
        <StatTile title={t("learn.summary.newWords")} value={number(summary.newWords)} />
        <StatTile title={t("learn.summary.reviewedWords")} value={number(summary.reviewedWords)} />
        <StatTile
          title={t("learn.summary.correct")}
          value={`${number(summary.correctAnswers)} / ${number(summary.answers)}`}
        />
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
