import { useQueryClient } from "@tanstack/react-query";
import { type KeyboardEvent, useCallback, useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { Navigate, useNavigate, useParams, useSearchParams } from "react-router";
import { AgentRunDialog } from "@/components/AgentRunDialog";
import { Button } from "@/components/Button";
import { EmptyState } from "@/components/EmptyState";
import { FormError } from "@/components/FormError";
import { Select } from "@/components/Select";
import { useToast } from "@/components/Toast";
import { ApiError, describeApiError } from "@/lib/api";
import { useAuth } from "@/lib/auth/AuthContext";
import { hasWriteRole } from "@/lib/auth/roles";
import { bookName } from "@/lib/bible/books";
import { DANIEL_BOOK_ID } from "@/lib/bible/lxx";
import { useBibleStructure } from "@/lib/bible/queries";
import {
  buildConfirmRequest,
  initialWordDraft,
  type LxxReviewBook,
  type LxxReviewUnit,
  lxxReviewKeys,
  safeSelection,
  selectionRange,
  useConfirmLxxSafe,
  useConfirmLxxUnit,
  useDiscardLxxUnit,
  useLxxReviewBooks,
  useLxxReviewChapter,
  useUnitDrafts,
  validateWordDraft,
  type WordDraftError,
} from "@/lib/maintainer/lxxReview";
import { useMediaQuery } from "@/lib/useMediaQuery";
import { ChapterList } from "./lxxReview/ChapterList";
import { DiscardDialog } from "./lxxReview/DiscardDialog";
import { SafeConfirmDialog } from "./lxxReview/SafeConfirmDialog";
import { UnitCard } from "./lxxReview/UnitCard";

export type LxxReviewFilter = "open" | "low" | "all";
export type LxxReviewForm = "og" | "th" | "both";

const FILTERS: LxxReviewFilter[] = ["open", "low", "all"];
const FORMS: LxxReviewForm[] = ["both", "og", "th"];
const DIGIT_WINDOW_MS = 600;

/** Units shown under the filter; `keptVisible` keeps units saved in this session in place (06a "States: saved"). */
function visibleUnits(
  units: readonly LxxReviewUnit[],
  filter: LxxReviewFilter,
  form: LxxReviewForm,
  keptVisible: ReadonlySet<number>,
): LxxReviewUnit[] {
  return units.filter((unit) => {
    if (form !== "both" && unit.textForm && unit.textForm !== form) return false;
    if (keptVisible.has(unit.lxxVerseId) || filter === "all") return true;
    if (filter === "low") return unit.words.some((w) => w.ai?.confidence === "low");
    return unit.open;
  });
}

/** First chapter of a book that has open work, else its first listed chapter, else 1. */
function firstChapterOf(book: LxxReviewBook | undefined): number {
  return book ? Math.min(...book.chapters.map((c) => c.chapter), Number.MAX_SAFE_INTEGER) : 1;
}

const toId = (value: string | undefined): number | undefined => {
  const n = Number(value);
  return value !== undefined && Number.isInteger(n) && n > 0 ? n : undefined;
};

/**
 * "LXX prüfen" (`/edit/lxx/:bookId?/:chapter?`, Admin/Maintainer, roadmap 0028.7f; design in
 * docs/roadmaps/0028-lxx-rahlfs/06a-review-page.md): Rahlfs chapters with open work on the left, the chapter's units with
 * their Hebrew verse and an editable word table on the right; confirm / discard per unit, safe chapter confirm, AI redo
 * for the selection (one run per contiguous block of verses), keyboard control (see the "Tastatur" list).
 */
export function LxxReviewPage() {
  const { t, i18n } = useTranslation();
  const { status, user } = useAuth();
  const canReview = hasWriteRole(user);
  const params = useParams();
  const navigate = useNavigate();
  const [search, setSearch] = useSearchParams();
  const queryClient = useQueryClient();
  const toast = useToast();
  const structure = useBibleStructure();
  const desktop = useMediaQuery("(min-width: 48rem)");

  const bookId = toId(params.bookId);
  const chapter = toId(params.chapter);
  const filter = (FILTERS as string[]).includes(search.get("f") ?? "")
    ? (search.get("f") as LxxReviewFilter)
    : "open";
  const form = (FORMS as string[]).includes(search.get("form") ?? "")
    ? (search.get("form") as LxxReviewForm)
    : "both";

  const books = useLxxReviewBooks(canReview);
  const chapterQuery = useLxxReviewChapter(
    canReview ? bookId : undefined,
    canReview ? chapter : undefined,
  );
  const units = chapterQuery.data?.units;
  const draftState = useUnitDrafts(units);
  const { drafts } = draftState;
  const confirm = useConfirmLxxUnit(bookId ?? 0, chapter ?? 0);
  const discard = useDiscardLxxUnit(bookId ?? 0, chapter ?? 0);
  const safe = useConfirmLxxSafe(bookId ?? 0, chapter ?? 0);

  const [selected, setSelected] = useState<ReadonlySet<number>>(new Set());
  const [keptVisible, setKeptVisible] = useState<ReadonlySet<number>>(new Set());
  const [activeWordId, setActiveWordId] = useState<number>();
  const [editing, setEditing] = useState(false);
  const [discarding, setDiscarding] = useState<LxxReviewUnit>();
  const [discardError, setDiscardError] = useState("");
  const [safeOpen, setSafeOpen] = useState(false);
  const [safeError, setSafeError] = useState("");
  const [aiOpen, setAiOpen] = useState(false);
  const [morphValid, setMorphValid] = useState<ReadonlyMap<string, boolean | undefined>>(new Map());
  const [focusEditor, setFocusEditor] = useState<"query" | "code">();
  const [focusRow, setFocusRow] = useState<number>();
  const queryRef = useRef<HTMLInputElement>(null);
  const codeRef = useRef<HTMLInputElement>(null);
  const lastSelected = useRef<number | undefined>(undefined);
  const digit = useRef<{ value: number; timer: ReturnType<typeof setTimeout> } | undefined>(
    undefined,
  );

  // Per-viewer state belongs to one chapter.
  // biome-ignore lint/correctness/useExhaustiveDependencies: reset on navigation only
  useEffect(() => {
    setSelected(new Set());
    setKeptVisible(new Set());
    setActiveWordId(undefined);
    setEditing(false);
    lastSelected.current = undefined;
  }, [bookId, chapter]);

  const onMorphologyValidity = useCallback(
    (wordId: number, code: string, valid: boolean | undefined) =>
      setMorphValid((prev) => {
        const key = `${wordId}|${code}`;
        if (prev.has(key) && prev.get(key) === valid) return prev;
        return new Map(prev).set(key, valid);
      }),
    [],
  );

  const shown = visibleUnits(units ?? [], filter, form, keptVisible);
  const visibleIds = new Set(shown.map((u) => u.lxxVerseId));
  const allWords = shown.flatMap((unit) => unit.words.map((word) => ({ unit, word })));
  const active = allWords.find((entry) => entry.word.id === activeWordId) ?? allWords[0];
  const effectiveActiveId = active?.word.id;
  const safeSel = safeSelection(units ?? [], drafts, visibleIds);
  const range = bookId ? selectionRange(bookId, units ?? [], selected) : null;
  const textForm = bookId === DANIEL_BOOK_ID && form !== "both" ? form : undefined;

  /** Validation errors per word and whether an edited parse code is still being checked. */
  const checkUnit = (unit: LxxReviewUnit) => {
    const draft = drafts.get(unit.lxxVerseId);
    const errors = new Map<number, WordDraftError[]>();
    let checking = false;
    for (const word of unit.words) {
      const wordDraft = draft?.words.get(word.id);
      if (!wordDraft) continue;
      const code = wordDraft.morphology.trim();
      const known = morphValid.get(`${word.id}|${code}`);
      const edited = code !== "" && code !== initialWordDraft(word).morphology.trim();
      if (edited && known === undefined) checking = true;
      const list = validateWordDraft(wordDraft, known);
      if (list.length > 0) errors.set(word.id, list);
    }
    return { errors, checking };
  };

  const keep = (...ids: number[]) => setKeptVisible((prev) => new Set([...prev, ...ids]));

  const confirmUnit = async (unit: LxxReviewUnit) => {
    const draft = drafts.get(unit.lxxVerseId);
    const check = checkUnit(unit);
    if (
      !draft ||
      check.errors.size > 0 ||
      check.checking ||
      draft.saveState === "saving" ||
      draft.saveState === "conflict"
    ) {
      return;
    }
    draftState.setSaveState(unit.lxxVerseId, "saving");
    try {
      const response = await confirm.mutateAsync(buildConfirmRequest(unit, draft));
      if (response.action === "") toast.show(t("lxxReview.done.unchanged"), "info");
      else if (response.proposalStatus === "edited-merged") {
        toast.show(t("lxxReview.done.edited", { count: response.confirmedWords }));
      } else toast.show(t("lxxReview.done.confirm", { count: response.confirmedWords }));
      keep(unit.lxxVerseId);
      draftState.setSaveState(unit.lxxVerseId, "saved");
    } catch (error) {
      if (error instanceof ApiError && error.status === 409) {
        draftState.setSaveState(unit.lxxVerseId, "conflict");
      } else {
        draftState.setSaveState(
          unit.lxxVerseId,
          "error",
          describeApiError(error, t("lxxReview.error.save")),
        );
      }
    }
  };

  const discardUnit = async () => {
    const unit = discarding;
    const proposalId = unit?.proposal?.id;
    if (!unit || proposalId === undefined) return;
    setDiscardError("");
    try {
      await discard.mutateAsync({ lxxVerseId: unit.lxxVerseId, proposalId });
      // The edits belonged to the discarded proposal: the card starts from the stored state again.
      draftState.resetUnit(unit.lxxVerseId);
      keep(unit.lxxVerseId);
      toast.show(t("lxxReview.done.discard"));
      setDiscarding(undefined);
    } catch (error) {
      if (error instanceof ApiError && error.status === 409) {
        draftState.setSaveState(unit.lxxVerseId, "conflict");
        setDiscarding(undefined);
      } else {
        setDiscardError(describeApiError(error, t("lxxReview.error.save")));
      }
    }
  };

  const confirmSafe = async () => {
    if (bookId === undefined || chapter === undefined || safeSel.units.length === 0) return;
    setSafeError("");
    const sent = safeSel.units;
    try {
      const response = await safe.mutateAsync({ bibleBookId: bookId, chapter, units: sent });
      toast.show(
        t("lxxReview.done.safe", {
          words: response.confirmedWords,
          units: response.units,
          low: response.skippedLow,
          other: response.skippedOther,
          merged: response.mergedProposals,
        }),
      );
      const stale = new Set(response.staleUnits);
      keep(...sent.map((u) => u.lxxVerseId));
      for (const unit of sent) {
        draftState.setSaveState(unit.lxxVerseId, stale.has(unit.lxxVerseId) ? "conflict" : "saved");
      }
      setSafeOpen(false);
    } catch (error) {
      setSafeError(describeApiError(error, t("lxxReview.error.save")));
    }
  };

  const reloadUnit = async (unit: LxxReviewUnit) => {
    draftState.resetUnit(unit.lxxVerseId);
    await queryClient.invalidateQueries({
      queryKey: lxxReviewKeys.chapter(bookId ?? 0, chapter ?? 0),
    });
  };

  const toggleSelected = (unit: LxxReviewUnit, on: boolean, shiftKey: boolean) => {
    const next = new Set(selected);
    const anchor = shiftKey ? shown.findIndex((u) => u.lxxVerseId === lastSelected.current) : -1;
    const here = shown.findIndex((u) => u.lxxVerseId === unit.lxxVerseId);
    const touched =
      anchor >= 0 ? shown.slice(Math.min(anchor, here), Math.max(anchor, here) + 1) : [unit];
    for (const u of touched) {
      if (on) next.add(u.lxxVerseId);
      else next.delete(u.lxxVerseId);
    }
    lastSelected.current = unit.lxxVerseId;
    setSelected(next);
  };

  // Latest state for the delayed digit key handler and the keyboard handler.
  const latest = useRef({ active, draftState, confirmUnit });
  latest.current = { active, draftState, confirmUnit };

  const toggleHebrewNumber = useCallback((n: number) => {
    const { active: entry, draftState: state } = latest.current;
    const hebrew = entry?.unit.hebrew.find((h) => h.number === n);
    if (entry && hebrew) state.toggleHebrew(entry.unit.lxxVerseId, entry.word.id, hebrew.id);
  }, []);

  useEffect(
    () => () => {
      if (digit.current) clearTimeout(digit.current.timer);
    },
    [],
  );

  // Focus handling after the render that opened an editor or moved the active word.
  useEffect(() => {
    if (focusEditor && editing) {
      (focusEditor === "query" ? queryRef : codeRef).current?.focus();
      setFocusEditor(undefined);
    }
  }, [focusEditor, editing]);
  useEffect(() => {
    if (focusRow !== undefined) {
      document.getElementById(`lxx-word-${focusRow}`)?.focus();
      setFocusRow(undefined);
    }
  }, [focusRow]);
  useEffect(() => {
    if (effectiveActiveId !== undefined) {
      document.getElementById(`lxx-word-${effectiveActiveId}`)?.scrollIntoView?.({
        block: "nearest",
      });
    }
  }, [effectiveActiveId]);

  const onKeyDown = (event: KeyboardEvent<HTMLElement>) => {
    const target = event.target as HTMLElement;
    const mod = event.ctrlKey || event.metaKey;
    if (event.key === "Enter" && mod) {
      if (active) {
        event.preventDefault();
        void confirmUnit(active.unit);
      }
      return;
    }
    if (event.key === "Escape") {
      if (editing) {
        event.preventDefault();
        setEditing(false);
        setFocusRow(effectiveActiveId);
      }
      return;
    }
    if (target.closest("input, select, textarea") || event.altKey || mod) return;
    const key = event.key;
    const index = allWords.findIndex((entry) => entry.word.id === effectiveActiveId);
    const move = (to: number) => {
      const entry = allWords[Math.max(0, Math.min(allWords.length - 1, to))];
      if (!entry) return;
      event.preventDefault();
      setActiveWordId(entry.word.id);
      setFocusRow(entry.word.id);
    };
    if (key === "j" || key === "ArrowDown") move(index + 1);
    else if (key === "k" || key === "ArrowUp") move(index - 1);
    else if ((key === "e" || key === "Enter") && active) {
      if (key === "Enter" && target.closest("button, a, summary")) return;
      event.preventDefault();
      setEditing(true);
      setFocusEditor("query");
    } else if (key === "p" && active) {
      event.preventDefault();
      setEditing(true);
      setFocusEditor("code");
    } else if (key === "x" && active) {
      event.preventDefault();
      toggleSelected(active.unit, !selected.has(active.unit.lxxVerseId), false);
    } else if (/^\d$/.test(key)) {
      event.preventDefault();
      const value = Number(key);
      const pending = digit.current;
      if (pending) {
        clearTimeout(pending.timer);
        digit.current = undefined;
        toggleHebrewNumber(pending.value * 10 + value);
      } else if (value === 0) {
        toggleHebrewNumber(10);
      } else {
        digit.current = {
          value,
          timer: setTimeout(() => {
            digit.current = undefined;
            toggleHebrewNumber(value);
          }, DIGIT_WINDOW_MS),
        };
      }
    }
  };

  if (status === "loading") return null;
  if (status === "anonymous") return <Navigate to="/login" replace />;
  if (!canReview) {
    return (
      <section className="mx-auto flex w-full max-w-4xl flex-col gap-4 p-4">
        <p role="alert" className="text-fg-muted">
          {t("admin.forbidden")}
        </p>
      </section>
    );
  }

  const searchSuffix = search.toString() ? `?${search.toString()}` : "";
  const bookList = books.data ?? [];
  if (books.data && bookList.length > 0 && bookId === undefined) {
    const first = bookList[0];
    return (
      <Navigate
        to={`/edit/lxx/${first?.bibleBookId}/${firstChapterOf(first)}${searchSuffix}`}
        replace
      />
    );
  }
  if (bookId !== undefined && chapter === undefined && books.data) {
    const target = bookList.find((b) => b.bibleBookId === bookId);
    return <Navigate to={`/edit/lxx/${bookId}/${firstChapterOf(target)}${searchSuffix}`} replace />;
  }

  const book = structure.data?.find((b) => b.id === bookId);
  const setParam = (key: string, value: string) => {
    const next = new URLSearchParams(search);
    next.set(key, value);
    setSearch(next, { replace: true });
  };
  const aiButton = (
    <Button
      variant="secondary"
      size="sm"
      disabled={selected.size === 0}
      onClick={() => setAiOpen(true)}
    >
      {t("lxxReview.action.aiRedo", { count: selected.size })}
    </Button>
  );
  const noBooks = books.data !== undefined && bookList.length === 0 && bookId === undefined;
  const notFound = chapterQuery.error instanceof ApiError && chapterQuery.error.status === 404;

  return (
    <section className="mx-auto flex w-full max-w-screen-2xl flex-col gap-4 p-4 md:h-full md:min-h-0 md:flex-row">
      {books.isError ? (
        <div className="flex flex-col gap-2 md:w-64">
          <FormError message={t("lxxReview.error.books")} />
          <Button variant="secondary" size="sm" onClick={() => void books.refetch()}>
            {t("lxxReview.action.retry")}
          </Button>
        </div>
      ) : noBooks ? (
        <EmptyState title={t("lxxReview.empty.books")}>{t("lxxReview.empty.booksHint")}</EmptyState>
      ) : (
        <ChapterList
          books={bookList}
          structure={structure.data}
          currentBookId={bookId}
          currentChapter={chapter}
          search={searchSuffix}
          loading={books.isPending}
          onNavigate={(b, c) => navigate(`/edit/lxx/${b}/${c}${searchSuffix}`)}
        />
      )}

      {/* biome-ignore lint/a11y/noStaticElementInteractions: keyboard shortcuts of the review page (06a "Keyboard"), the focusable parts are the rows, buttons and fields inside */}
      <div
        className="flex min-w-0 flex-1 flex-col gap-4 md:min-h-0 md:overflow-y-auto"
        onKeyDown={onKeyDown}
      >
        <header className="flex flex-col gap-2">
          <h1 className="text-2xl font-semibold">{t("lxxReview.title")}</h1>
          <p className="text-fg-muted">{t("lxxReview.intro")}</p>
          <div className="flex flex-wrap items-end gap-2">
            <Select
              label={t("lxxReview.filter.label")}
              size="sm"
              value={filter}
              onChange={(value) => setParam("f", value)}
              options={FILTERS.map((f) => ({ value: f, label: t(`lxxReview.filter.${f}`) }))}
            />
            {bookId === DANIEL_BOOK_ID ? (
              <Select
                label={t("lxxReview.form.label")}
                size="sm"
                value={form}
                onChange={(value) => setParam("form", value)}
                options={FORMS.map((f) => ({ value: f, label: t(`lxxReview.form.${f}`) }))}
              />
            ) : null}
            <Button
              variant="secondary"
              size="sm"
              disabled={safeSel.units.length === 0}
              onClick={() => {
                setSafeError("");
                setSafeOpen(true);
              }}
            >
              {t("lxxReview.action.confirmSafe")}
            </Button>
            {desktop ? aiButton : null}
          </div>
          {range ? (
            <p className="text-sm text-fg-muted">
              {t("lxxReview.ai.selection", { count: selected.size, blocks: range.blocks.length })}
              {range.confirmedUnits > 0
                ? ` ${t("lxxReview.ai.skipConfirmed", { count: range.confirmedUnits })}`
                : ""}
              {textForm
                ? ` ${t("lxxReview.ai.daniel", { form: t(`lxxReview.form.${textForm}`) })}`
                : ""}
            </p>
          ) : null}
          <details className="text-sm text-fg-muted">
            <summary className="cursor-pointer">{t("lxxReview.keys.title")}</summary>
            <ul className="mt-1 list-disc pl-5">
              {(["move", "edit", "hebrew", "select", "close", "confirm"] as const).map((key) => (
                <li key={key}>{t(`lxxReview.keys.${key}`)}</li>
              ))}
            </ul>
          </details>
        </header>

        {chapterQuery.isError ? (
          <FormError
            message={notFound ? t("lxxReview.error.notFound") : t("lxxReview.error.chapter")}
          />
        ) : chapter !== undefined && chapterQuery.isPending ? (
          <EmptyState variant="comingSoon" skeletonRows={3}>
            {null}
          </EmptyState>
        ) : units && units.length === 0 ? (
          <EmptyState title={t("lxxReview.empty.chapter")}>{null}</EmptyState>
        ) : units && shown.length === 0 ? (
          <EmptyState title={t("lxxReview.empty.open")}>
            <Button variant="secondary" size="sm" onClick={() => setParam("f", "all")}>
              {t("lxxReview.action.showAll")}
            </Button>
          </EmptyState>
        ) : (
          shown.map((unit) => {
            const check = checkUnit(unit);
            return (
              <UnitCard
                key={unit.lxxVerseId}
                unit={unit}
                draft={drafts.get(unit.lxxVerseId)}
                errors={check.errors}
                checking={check.checking}
                selected={selected.has(unit.lxxVerseId)}
                activeWordId={effectiveActiveId}
                editing={editing}
                queryRef={queryRef}
                codeRef={codeRef}
                onSelect={(on, shiftKey) => toggleSelected(unit, on, shiftKey)}
                onActivate={setActiveWordId}
                onChangeWord={(wordId, patch) =>
                  draftState.updateWord(unit.lxxVerseId, wordId, patch)
                }
                onToggleHebrew={(hebrewId) =>
                  effectiveActiveId !== undefined &&
                  draftState.toggleHebrew(unit.lxxVerseId, effectiveActiveId, hebrewId)
                }
                onMorphologyValidity={onMorphologyValidity}
                onResetWord={(wordId) => draftState.resetWord(unit.lxxVerseId, wordId)}
                onConfirm={() => void confirmUnit(unit)}
                onDiscard={() => {
                  setDiscardError("");
                  setDiscarding(unit);
                }}
                onReload={() => void reloadUnit(unit)}
              />
            );
          })
        )}
        {!desktop && selected.size > 0 ? (
          <div className="sticky bottom-0 flex justify-end border-t border-border bg-surface p-2">
            {aiButton}
          </div>
        ) : null}
      </div>

      <DiscardDialog
        open={discarding !== undefined}
        unitRef={discarding?.ref ?? ""}
        pending={discard.isPending}
        error={discardError}
        onConfirm={() => void discardUnit()}
        onClose={() => setDiscarding(undefined)}
      />
      <SafeConfirmDialog
        open={safeOpen}
        counts={safeSel.counts}
        pending={safe.isPending}
        error={safeError}
        onConfirm={() => void confirmSafe()}
        onClose={() => setSafeOpen(false)}
      />
      {bookId !== undefined && chapter !== undefined && range && aiOpen ? (
        <AgentRunDialog
          open
          onClose={() => {
            setAiOpen(false);
            void queryClient.invalidateQueries({
              queryKey: lxxReviewKeys.chapter(bookId, chapter),
            });
            void queryClient.invalidateQueries({ queryKey: lxxReviewKeys.books });
          }}
          initial={range.initial}
          blocks={range.blocks}
          textForm={textForm}
          bookName={book ? bookName(book, i18n.language) : String(bookId)}
          modes={["lxx-tag"]}
          reviewHref={() => `/edit/lxx/${bookId}/${chapter}`}
        />
      ) : null}
    </section>
  );
}
