import { useQueryClient } from "@tanstack/react-query";
import {
  Fragment,
  useCallback,
  useEffect,
  useId,
  useLayoutEffect,
  useMemo,
  useRef,
  useState,
} from "react";
import { useTranslation } from "react-i18next";
import { Link, useLocation, useNavigate, useParams } from "react-router";
import { AgentRunDialog } from "@/components/AgentRunDialog";
import { BibleChapterPicker } from "@/components/BibleChapterPicker";
import { Button, buttonClasses } from "@/components/Button";
import { CrossReferenceList } from "@/components/CrossReferenceList";
import { HeaderPickerBar } from "@/components/HeaderPickerBar";
import { WordInfoCard } from "@/components/HeaderWordInfo";
import { LevelSlider } from "@/components/LevelSlider";
import { MappingEditor, type MappingSaveError } from "@/components/MappingEditor";
import { MarginStack } from "@/components/MarginStack";
import { OriginalVerse } from "@/components/OriginalVerse";
import { RevisionEditor } from "@/components/RevisionEditor";
import { type WordDetailCloseReason, WordDetailPopover } from "@/components/WordDetailPopover";
import { track, trackRevisionLevel } from "@/lib/analytics";
import { ApiError, describeApiErrors } from "@/lib/api";
import { useAuth } from "@/lib/auth/AuthContext";
import { hasWriteRole } from "@/lib/auth/roles";
import { adjacentChapter, bookName, chapterPath, parseChapterParams } from "@/lib/bible/books";
import { chapterWordsQuery, useBibleStructure, useChapterWords } from "@/lib/bible/queries";
import {
  nextSidePanelMode,
  type SidePanelMode,
  useChapterCrossReferences,
  useChapterOriginals,
  useSidePanelMode,
} from "@/lib/bible/sidePanel";
import type {
  BibleStructureBook,
  ChapterRef,
  CrossReference,
  HighlightKind,
  WordToken,
} from "@/lib/bible/types";
import { useSaveVerseMapping, useVerseMapping } from "@/lib/bible/verseMapping";
import { parseVerseSelection } from "@/lib/bible/verseSelection";
import { useWordDetail, wordDetailQuery, wordDetailSource } from "@/lib/bible/wordDetail";
import { buildHighlights, phraseWordIds, wordKey } from "@/lib/bible/words";
import { useSetHeaderInfo } from "@/lib/headerInfo";
import { reviewMarkers, toMappingData } from "@/lib/revision/mapping";
import { useRevisionMapping, useSaveRevisionMapping } from "@/lib/revision/queries";
import { useReaderLevel } from "@/lib/revision/readerLevel";
import { useMediaQuery } from "@/lib/useMediaQuery";
import { SidePanelRail } from "./chapter/SidePanelRail";
import { VerseSide } from "./chapter/VerseSide";

const HIGHLIGHT_CLASSES: Record<HighlightKind, string> = {
  same: "bg-mark-same",
  related: "bg-mark-related",
};
/** Words translated together with the open word; an underline so it combines with the hover highlight. */
/** Hover/focus must rest this long on a word before its detail is prefetched. */
const PREFETCH_DELAY_MS = 200;
const NO_IDS: ReadonlySet<number> = new Set();
const PHRASE_CLASSES = "underline decoration-accent decoration-2 underline-offset-4";

/** Words of the base text have a detail through their Elb1871 id, words of a revised verse through their revision word id. */
const hasDetailId = (word: WordToken) => word.id > 0 || word.revisionWordId !== undefined;

function isTypingTarget(target: EventTarget | null): boolean {
  if (!(target instanceof HTMLElement)) return false;
  return (
    target instanceof HTMLInputElement ||
    target instanceof HTMLTextAreaElement ||
    target instanceof HTMLSelectElement ||
    target.isContentEditable
  );
}

export function ChapterPage() {
  const { t, i18n } = useTranslation();
  const params = useParams();
  const navigate = useNavigate();
  const location = useLocation();
  const queryClient = useQueryClient();
  const canRevise = hasWriteRole(useAuth().user);
  const structure = useBibleStructure();
  const books = structure.data;

  const ref = parseChapterParams(params.bookId, params.chapter, books);
  const reader = useReaderLevel();
  const verses = useChapterWords(books && reader.ready ? ref : null, reader.level);
  const [hoveredKey, setHoveredKey] = useState<string>();
  const [selectedKey, setSelectedKey] = useState<string>();
  const [editingVerse, setEditingVerse] = useState<number>();
  const [verseView, setVerseView] = useState<"original" | "align">("original");
  /** Verse whose revision panel is open; independent of the original-text / mapping view (eye icon). */
  const [reviseVerse, setReviseVerse] = useState<number>();
  /** Range the "KI neu zuordnen" dialog was opened with (a verse from the mapping editor, the chapter from its header). */
  const [aiRange, setAiRange] = useState<{ fromVerse: number; toVerse?: number }>();
  const [showLemma, setShowLemma] = useState(false);
  const [showStrongs, setShowStrongs] = useState(false);
  const [showMorphology, setShowMorphology] = useState(false);
  const setHeaderInfo = useSetHeaderInfo();
  // From 1024px the hover info and the word detail live in the page margin next to the text.
  const margin = useMediaQuery("(min-width: 64rem)");
  const colRef = useRef<HTMLDivElement>(null);
  const editTriggerRef = useRef<HTMLElement | null>(null);
  const revisionTriggerRef = useRef<HTMLElement | null>(null);
  const triggerRef = useRef<HTMLElement | null>(null);
  const popoverId = useId();
  const selectedVerse = verses.data?.find((v) => v.words.some((w) => wordKey(w) === selectedKey));
  const selectedWord = selectedVerse?.words.find((w) => wordKey(w) === selectedKey);
  const detail = useWordDetail(selectedWord, selectedVerse?.words);
  const phrase = useMemo(
    () => phraseWordIds(selectedKey !== undefined ? detail.data : undefined),
    [selectedKey, detail.data],
  );
  const hoveredVerse = verses.data?.find((v) => v.words.some((w) => wordKey(w) === hoveredKey));
  const hoveredWordRaw = hoveredVerse?.words.find((w) => wordKey(w) === hoveredKey);
  const hoveredWord = hoveredWordRaw && hasDetailId(hoveredWordRaw) ? hoveredWordRaw : undefined;
  const hoveredDetail = useWordDetail(hoveredWord, hoveredVerse?.words);
  const hoveredReading = hoveredWord ? hoveredDetail.data?.readings[0] : undefined;
  useEffect(() => {
    if (!hoveredReading) {
      setHeaderInfo(null);
      return;
    }
    setHeaderInfo({
      original: hoveredReading.original,
      lemma: hoveredReading.lemma,
      strongs: hoveredReading.strongs,
      morphology: hoveredReading.morphology,
      language: hoveredReading.language,
      inMargin: true,
    });
  }, [hoveredReading, setHeaderInfo]);
  const hoveredInfo = useMemo(
    () =>
      hoveredReading && {
        original: hoveredReading.original,
        lemma: hoveredReading.lemma,
        strongs: hoveredReading.strongs,
        morphology: hoveredReading.morphology,
        language: hoveredReading.language,
      },
    [hoveredReading],
  );
  useEffect(() => () => setHeaderInfo(null), [setHeaderInfo]);
  const activeKey = hoveredKey ?? selectedKey;
  const highlights = useMemo(() => {
    const data = verses.data ?? [];
    const active = data.flatMap((v) => v.words).find((w) => wordKey(w) === activeKey);
    return buildHighlights(active, data);
  }, [verses.data, activeKey]);

  const verseRef = ref && editingVerse !== undefined ? { ...ref, verse: editingVerse } : null;
  // A verse revised at the reader's level has its own mapping (RevisionWords ids); the legacy editor is only for base verses.
  const revisedId = verses.data?.find((v) => v.verse === editingVerse)?.revised?.revisionVerseId;
  const baseMapping = useVerseMapping(revisedId !== undefined ? null : verseRef);
  const revisionMapping = useRevisionMapping(revisedId ?? null);
  const mapping = revisedId !== undefined ? revisionMapping : baseMapping;
  const mappingData = useMemo(
    () =>
      revisedId !== undefined
        ? revisionMapping.data && toMappingData(revisionMapping.data, canRevise)
        : baseMapping.data,
    [revisedId, revisionMapping.data, baseMapping.data, canRevise],
  );
  const reviewWords = useMemo(
    () =>
      revisedId !== undefined && revisionMapping.data
        ? reviewMarkers(revisionMapping.data)
        : undefined,
    [revisedId, revisionMapping.data],
  );
  const saveBaseMapping = useSaveVerseMapping();
  const saveRevisionMapping = useSaveRevisionMapping();
  const saveMapping = revisedId !== undefined ? saveRevisionMapping : saveBaseMapping;
  const [hoveredOriginalId, setHoveredOriginalId] = useState<number>();
  /** German and original word ids linked to the hovered word in the open original-text view. */
  const linked = useMemo(() => {
    const german = new Set<number>();
    const original = new Set<number>();
    const links = verseView === "original" ? (mappingData?.links ?? []) : [];
    // revision words are linked by their revision word id, base words by the Elb1871 id
    const hoveredLinkId = revisedId !== undefined ? hoveredWord?.revisionWordId : hoveredWord?.id;
    for (const l of links) {
      if (l.stepWordId === undefined) continue;
      if (hoveredLinkId === l.elbWordId || hoveredOriginalId === l.stepWordId) {
        german.add(l.elbWordId);
        original.add(l.stepWordId);
      }
    }
    return { german, original };
  }, [mappingData, verseView, hoveredWord, hoveredOriginalId, revisedId]);

  // Left margin (from 120rem): TSK / Grundtext next to every verse, one request per chapter and kind.
  const wide = useMediaQuery("(min-width: 120rem)");
  const [sideMode, setSideMode] = useSidePanelMode();
  const showTsk = wide && sideMode === "tsk";
  const showOriginal = wide && sideMode === "original";
  // The eye panel gets its own TSK list wherever the side panel does not show it.
  const eyeOriginalOpen = editingVerse !== undefined && verseView === "original";
  const crossReferences = useChapterCrossReferences(ref, showTsk || eyeOriginalOpen);
  const originals = useChapterOriginals(ref, showOriginal);
  const referencesByVerse = useMemo(
    () => new Map(crossReferences.data?.verses.map((v) => [v.verse, v.references])),
    [crossReferences.data],
  );
  const originalsByVerse = useMemo(
    () => new Map(originals.data?.verses.map((v) => [v.verse, v])),
    [originals.data],
  );
  const [expandedVerse, setExpandedVerse] = useState<number>();
  const onExpandedChange = useCallback((verse: number, expanded: boolean) => {
    setExpandedVerse((current) => (expanded ? verse : current === verse ? undefined : current));
  }, []);
  const [hoveredSide, setHoveredSide] = useState<{ verse: number; id: number }>();
  const onHoverOriginal = useCallback((verse: number, id: number | undefined) => {
    setHoveredSide(id === undefined ? undefined : { verse, id });
  }, []);
  const sideActive = showOriginal ? hoveredSide : undefined;
  /** German and original words linked to the hovered original word or (non-revised) German word, within one verse. */
  const sideLinks = useMemo(() => {
    const german = new Set<number>();
    const original = new Set<number>();
    const verse = sideActive?.verse ?? (showOriginal ? hoveredVerse?.verse : undefined);
    const links = verse !== undefined ? (originalsByVerse.get(verse)?.links ?? []) : [];
    const hoveredGerman =
      sideActive === undefined && hoveredWordRaw?.revisionWordId === undefined
        ? hoveredWordRaw?.id
        : undefined;
    for (const l of links) {
      if (l.originalId === sideActive?.id || (hoveredGerman && l.germanId === hoveredGerman)) {
        german.add(l.germanId);
        original.add(l.originalId);
      }
    }
    return { verse, german, original };
  }, [sideActive, showOriginal, hoveredVerse, hoveredWordRaw, originalsByVerse]);
  // What the right margin shows on hover: the German word's reading (the source-language words show their own tooltip).
  const infoData = hoveredInfo;
  const infoVerse = hoveredVerse;

  // A mode switch changes row heights: keep the verse at the top of the viewport where it was.
  const scrollAnchor = useRef<{ row: Element; top: number }>(undefined);
  const changeSideMode = useCallback(
    (next: SidePanelMode) => {
      if (next === sideMode) return;
      const rows = Array.from(colRef.current?.querySelectorAll(".verse-row") ?? []);
      const row = rows.find((r) => r.getBoundingClientRect().bottom > 0);
      scrollAnchor.current = row ? { row, top: row.getBoundingClientRect().top } : undefined;
      setExpandedVerse(undefined);
      setHoveredSide(undefined);
      setSideMode(next);
    },
    [sideMode, setSideMode],
  );
  // biome-ignore lint/correctness/useExhaustiveDependencies: runs once per mode switch
  useLayoutEffect(() => {
    const anchor = scrollAnchor.current;
    scrollAnchor.current = undefined;
    if (!anchor) return;
    const delta = anchor.row.getBoundingClientRect().top - anchor.top;
    if (delta !== 0) window.scrollBy(0, delta);
  }, [sideMode]);
  const sideStatus = !wide
    ? "idle"
    : sideMode === "tsk"
      ? crossReferences.isError
        ? "error"
        : crossReferences.isPending
          ? "loading"
          : crossReferences.data.verses.length === 0
            ? "empty"
            : "idle"
      : sideMode === "original"
        ? originals.isError
          ? "error"
          : originals.isPending
            ? "loading"
            : "idle"
        : "idle";

  function openEditor(verse: number, trigger: HTMLElement | null) {
    saveMapping.reset();
    editTriggerRef.current = trigger;
    setSelectedKey(undefined);
    setVerseView("align");
    setEditingVerse(verse);
  }

  /** Eye icon: original text first, a second click opens the mapping, a third closes; Ctrl/Cmd+click jumps to the mapping. */
  function cycleVerse(verse: number, trigger: HTMLElement, direct: boolean) {
    if (editingVerse !== verse) {
      if (direct) {
        openEditor(verse, trigger);
        return;
      }
      saveMapping.reset();
      editTriggerRef.current = trigger;
      setSelectedKey(undefined);
      setVerseView("original");
      setEditingVerse(verse);
      return;
    }
    if (verseView === "original") {
      setVerseView("align");
      return;
    }
    closeEditor();
  }

  /** Pen icon: opens or closes the revision panel of the verse. */
  function toggleRevision(verse: number, trigger: HTMLElement) {
    if (reviseVerse === verse) {
      closeRevision();
      return;
    }
    revisionTriggerRef.current = trigger;
    setSelectedKey(undefined);
    setReviseVerse(verse);
  }

  function closeRevision() {
    setReviseVerse(undefined);
    revisionTriggerRef.current?.focus();
  }

  function closeEditor() {
    setEditingVerse(undefined);
    saveMapping.reset();
    editTriggerRef.current?.focus();
  }

  function saveError(): MappingSaveError | null {
    if (!saveMapping.isError) return null;
    const status = saveMapping.error instanceof ApiError ? saveMapping.error.status : 0;
    if (status === 400) return "validation";
    return status === 403 ? "forbidden" : "failed";
  }

  const prefetchTimer = useRef<ReturnType<typeof setTimeout>>(undefined);
  function startIntent(word: WordToken, verseWords: WordToken[]) {
    setHoveredKey(wordKey(word));
    clearTimeout(prefetchTimer.current);
    if (!hasDetailId(word)) return;
    prefetchTimer.current = setTimeout(() => {
      void queryClient.prefetchQuery(wordDetailQuery(word, verseWords));
    }, PREFETCH_DELAY_MS);
  }
  function endIntent() {
    setHoveredKey(undefined);
    clearTimeout(prefetchTimer.current);
  }
  useEffect(() => () => clearTimeout(prefetchTimer.current), []);

  function toggle(word: WordToken, element: HTMLElement) {
    if (!hasDetailId(word)) return;
    if (selectedKey === wordKey(word)) {
      setSelectedKey(undefined);
      return;
    }
    triggerRef.current = element;
    setSelectedKey(wordKey(word));
  }

  function close(reason: WordDetailCloseReason) {
    setSelectedKey(undefined);
    if (reason !== "outside") triggerRef.current?.focus();
  }

  const prev = books && ref ? adjacentChapter(books, ref, -1) : null;
  const next = books && ref ? adjacentChapter(books, ref, 1) : null;
  const prevPath = prev ? chapterPath(prev) : null;
  const nextPath = next ? chapterPath(next) : null;

  useEffect(() => {
    function onKeyDown(event: KeyboardEvent) {
      if (event.defaultPrevented) return;
      if (event.altKey || event.ctrlKey || event.metaKey || event.shiftKey) return;
      if (isTypingTarget(event.target)) return;
      if (event.target instanceof Element && event.target.closest("[data-mapping-editor]")) return;
      if (event.key === "g" && wide) {
        changeSideMode(nextSidePanelMode(sideMode));
        return;
      }
      if (selectedKey !== undefined) return;
      if (event.key === "ArrowLeft" && prevPath) navigate(prevPath);
      if (event.key === "ArrowRight" && nextPath) navigate(nextPath);
    }
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, [prevPath, nextPath, navigate, selectedKey, wide, sideMode, changeSideMode]);

  const bookId = ref?.bookId;
  const chapterNumber = ref?.chapter;
  // biome-ignore lint/correctness/useExhaustiveDependencies: closing on every chapter change is the point
  useEffect(() => {
    setSelectedKey(undefined);
    setEditingVerse(undefined);
    setReviseVerse(undefined);
  }, [bookId, chapterNumber]);

  const testament = books?.find((b) => b.id === bookId)?.testament;
  const trackedChapter = useRef("");
  useEffect(() => {
    const key = `${bookId}:${chapterNumber}`;
    if (!testament || trackedChapter.current === key) return;
    trackedChapter.current = key;
    track("chapter-open", { testament });
  }, [bookId, chapterNumber, testament]);

  useEffect(() => {
    if (verses.isSuccess && next) {
      void queryClient.prefetchQuery(chapterWordsQuery(next.bookId, next.chapter, reader.level));
    }
  }, [verses.isSuccess, next, queryClient, reader.level]);

  const hash = location.hash;
  useEffect(() => {
    if (!verses.isSuccess || !hash.startsWith("#v")) return;
    document.getElementById(hash.slice(1))?.scrollIntoView();
  }, [verses.isSuccess, hash]);
  // `?v=1-5,7,9-10` (deep link from a reference) marks those verses; the hash scrolls to the first one
  const passageVerses = useMemo(
    () => parseVerseSelection(new URLSearchParams(location.search).get("v")),
    [location.search],
  );

  // `?map=<verse>` (data-quality jump) opens the mapping editor of that verse once per chapter visit
  const mapParam = new URLSearchParams(location.search).get("map");
  const mapVerse = mapParam && /^\d+$/.test(mapParam) ? Number(mapParam) : undefined;
  // `?revise=<level>` (revision overview jump) switches the reader to that level before the verse opens
  const reviseParam = new URLSearchParams(location.search).get("revise");
  const reviseLevel = reviseParam && /^\d+$/.test(reviseParam) ? Number(reviseParam) : undefined;
  const reviseKnown =
    reviseLevel !== undefined && reader.levels.some((l) => l.level === reviseLevel);
  const openedMapKey = useRef<string | undefined>(undefined);
  const { setLevel: setReaderLevel } = reader;
  useEffect(() => {
    if (reviseKnown && reviseLevel !== reader.level) setReaderLevel(reviseLevel);
  }, [reviseKnown, reviseLevel, reader.level, setReaderLevel]);
  // biome-ignore lint/correctness/useExhaustiveDependencies: openEditor only resets local state
  useEffect(() => {
    if (!verses.isSuccess || mapVerse === undefined) return;
    if (reviseKnown && reviseLevel !== reader.level) return; // wait for the chapter at that level
    const key = `${bookId}:${chapterNumber}:${mapVerse}`;
    if (openedMapKey.current === key || !verses.data.some((v) => v.verse === mapVerse)) return;
    openedMapKey.current = key;
    openEditor(mapVerse, null);
  }, [
    verses.isSuccess,
    verses.data,
    bookId,
    chapterNumber,
    mapVerse,
    reviseKnown,
    reviseLevel,
    reader.level,
  ]);

  if (structure.isError) {
    return (
      <div role="alert" className="flex flex-wrap items-center gap-4">
        <p>{t("bible.structureError")}</p>
        <Button variant="secondary" onClick={() => structure.refetch()}>
          {t("bible.retry")}
        </Button>
      </div>
    );
  }

  if (!books) {
    return <p className="text-fg-muted">{t("bible.chapter.loading")}</p>;
  }

  if (!ref) {
    return (
      <>
        <BibleChapterPicker books={books} />
        <h1 className="mt-6 font-scripture text-3xl font-semibold">
          {t("bible.chapter.notFound")}
        </h1>
        <Link to="/bible" className="mt-4 inline-block text-accent underline">
          {t("bible.chapter.toOverview")}
        </Link>
      </>
    );
  }

  const book = books.find((b) => b.id === ref.bookId) as BibleStructureBook;
  const language = i18n.language;
  const reference = (r: ChapterRef) => {
    const target = books.find((b) => b.id === r.bookId) as BibleStructureBook;
    return `${bookName(target, language)} ${r.chapter}`;
  };
  const title = `${reference(ref)} – ${t("app.name")}`;
  const revisedLabel = (level: number) =>
    t("bible.revision.revisedMark", {
      level: reader.levels.find((l) => l.level === level)?.name ?? level,
    });

  function renderLink(
    target: ChapterRef | null,
    direction: "previous" | "next",
    withLabel: boolean,
  ) {
    const arrow = direction === "previous" ? "‹" : "›";
    if (!target) {
      return (
        <span
          aria-disabled="true"
          className={`${buttonClasses({ variant: "ghost", size: "md" })} opacity-50`}
        >
          <span aria-hidden="true" className="text-4xl leading-none">
            {arrow}
          </span>
          <span className="sr-only">
            {t(direction === "previous" ? "bible.chapter.noPrevious" : "bible.chapter.noNext")}
          </span>
        </span>
      );
    }
    const label = reference(target);
    return (
      <Link
        to={chapterPath(target)}
        aria-label={t(`bible.chapter.${direction}`, { reference: label })}
        className={buttonClasses({ variant: "ghost", size: "md" })}
      >
        {direction === "next" && withLabel && label}
        <span aria-hidden="true" className="text-4xl leading-none">
          {arrow}
        </span>
        {direction === "previous" && withLabel && label}
      </Link>
    );
  }

  const pinnedPopover = selectedWord && (
    <WordDetailPopover
      id={popoverId}
      word={selectedWord}
      status={detail.status}
      detail={detail.data}
      books={books}
      isSample={wordDetailSource === "mock"}
      onRetry={() => detail.refetch()}
      onClose={close}
      variant={margin ? "margin" : "popover"}
      verse={
        ref && selectedVerse
          ? {
              bookId: ref.bookId,
              chapter: ref.chapter,
              verse: selectedVerse.verse,
              level: reader.level,
            }
          : undefined
      }
      onEditMapping={() => {
        if (selectedVerse) openEditor(selectedVerse.verse, triggerRef.current);
      }}
    />
  );

  return (
    <>
      <title>{title}</title>
      <HeaderPickerBar>
        <div className="min-w-0 flex-1 laptop:w-96 laptop:flex-none">
          <BibleChapterPicker books={books} currentBookId={book.id} currentChapter={ref.chapter} />
        </div>
        <div className="flex h-10 shrink-0 items-center">
          <LevelSlider
            levels={reader.levels}
            value={reader.level}
            onChange={(level) => {
              reader.setLevel(level);
              trackRevisionLevel(level);
            }}
            label={t("bible.revision.levelSlider")}
          />
        </div>
      </HeaderPickerBar>
      <div ref={colRef} className="reader-col mt-6">
        <header className="flex items-center justify-between gap-4">
          {renderLink(prev, "previous", false)}
          <h1 className="text-center font-scripture text-3xl font-semibold md:text-4xl">
            {reference(ref)}
          </h1>
          {renderLink(next, "next", false)}
        </header>
        {canRevise && (
          <div className="mt-2 flex justify-center">
            <Button variant="ghost" size="sm" onClick={() => setAiRange({ fromVerse: 1 })}>
              {t("agent.chapter.aiRedo")}
            </Button>
          </div>
        )}
        {aiRange && (
          <AgentRunDialog
            open
            onClose={() => setAiRange(undefined)}
            bookName={bookName(book, language)}
            initial={{
              book: ref.bookId,
              fromChapter: ref.chapter,
              fromVerse: aiRange.fromVerse,
              toChapter: ref.chapter,
              toVerse: aiRange.toVerse,
            }}
          />
        )}
        {wide && (
          <SidePanelRail
            mode={sideMode}
            onModeChange={changeSideMode}
            status={sideStatus}
            onRetry={() =>
              void (sideMode === "tsk" ? crossReferences.refetch() : originals.refetch())
            }
          />
        )}
        <article
          lang="de"
          aria-busy={verses.isLoading}
          className="mt-6 font-scripture text-lg leading-relaxed md:text-xl laptop:text-2xl"
        >
          {verses.isLoading && <p className="text-fg-muted">{t("bible.chapter.loading")}</p>}
          {verses.isError && (
            <div role="alert" className="flex flex-wrap items-center gap-4">
              <p>{t("bible.chapter.error")}</p>
              <Button variant="secondary" onClick={() => verses.refetch()}>
                {t("bible.retry")}
              </Button>
            </div>
          )}
          {verses.isSuccess && verses.data.length === 0 && (
            <p className="text-fg-muted">{t("bible.chapter.empty")}</p>
          )}
          {verses.isSuccess &&
            verses.data.map((v) => {
              const editing = editingVerse === v.verse;
              const revising = reviseVerse === v.verse;
              return (
                <div
                  key={v.verse}
                  id={`v${v.verse}`}
                  className={`verse-row relative mt-2 scroll-mt-32 ${passageVerses.has(v.verse) ? "bg-mark-passage" : ""} ${v.revised ? "before:absolute before:inset-y-0 before:-left-3 before:w-1 before:bg-accent" : ""}`}
                  {...(v.revised && {
                    role: "group",
                    title: revisedLabel(v.revised.level),
                    "aria-label": revisedLabel(v.revised.level),
                  })}
                >
                  <div className="verse-main">
                    <p>
                      <sup className="mr-1 text-xs text-fg-muted">{v.verse}</sup>
                      {v.words.map((word) => {
                        const key = wordKey(word);
                        const hasDetail = hasDetailId(word);
                        const linkId = word.revisionWordId ?? word.id;
                        const interactive = word.senses.length > 0 && hasDetail;
                        const kind = highlights.get(key);
                        return (
                          <Fragment key={key}>
                            <Button
                              variant="inline"
                              data-word-trigger=""
                              aria-haspopup={hasDetail ? "dialog" : undefined}
                              aria-expanded={hasDetail ? selectedKey === key : undefined}
                              aria-controls={selectedKey === key ? popoverId : undefined}
                              aria-label={t("bible.wordDetail.trigger", { word: word.text })}
                              onClick={(e) => toggle(word, e.currentTarget)}
                              onMouseEnter={
                                interactive ? () => startIntent(word, v.words) : undefined
                              }
                              onMouseLeave={interactive ? endIntent : undefined}
                              onFocus={interactive ? () => startIntent(word, v.words) : undefined}
                              onBlur={interactive ? endIntent : undefined}
                              className={`${(hasDetail && linked.german.has(linkId)) || (v.verse === sideLinks.verse && word.revisionWordId === undefined && sideLinks.german.has(word.id)) ? "bg-mark-linked" : kind ? HIGHLIGHT_CLASSES[kind] : ""} ${hasDetail && phrase.has(linkId) ? PHRASE_CLASSES : ""} ${selectedKey === key ? "word-anchor" : ""}`}
                            >
                              {word.text}
                            </Button>{" "}
                          </Fragment>
                        );
                      })}
                      <Button
                        variant="ghost"
                        size="sm"
                        className="map-trigger"
                        aria-expanded={editing}
                        aria-controls={editing ? `mapping-v${v.verse}` : undefined}
                        aria-label={t(
                          !editing
                            ? "bible.mapping.triggerLabel"
                            : verseView === "original"
                              ? "bible.mapping.triggerLabelOriginal"
                              : "bible.mapping.triggerLabelAlign",
                          { verse: v.verse },
                        )}
                        onClick={(e) =>
                          cycleVerse(v.verse, e.currentTarget, e.ctrlKey || e.metaKey)
                        }
                      >
                        <svg
                          aria-hidden="true"
                          viewBox="0 0 24 24"
                          className="size-5"
                          fill="none"
                          stroke="currentColor"
                          strokeWidth="2"
                          strokeLinecap="round"
                          strokeLinejoin="round"
                        >
                          <path d="M2 12s3.6-7 10-7 10 7 10 7-3.6 7-10 7S2 12 2 12Z" />
                          <circle cx="12" cy="12" r="3" />
                        </svg>
                      </Button>
                      {canRevise && (
                        <Button
                          variant="ghost"
                          size="sm"
                          className="map-trigger"
                          aria-expanded={revising}
                          aria-controls={revising ? `revision-v${v.verse}` : undefined}
                          aria-label={t(
                            revising
                              ? "bible.revision.triggerLabelOpen"
                              : "bible.revision.triggerLabel",
                            { verse: v.verse },
                          )}
                          onClick={(e) => toggleRevision(v.verse, e.currentTarget)}
                        >
                          <svg
                            aria-hidden="true"
                            viewBox="0 0 24 24"
                            className="size-5"
                            fill="none"
                            stroke="currentColor"
                            strokeWidth="2"
                            strokeLinecap="round"
                            strokeLinejoin="round"
                          >
                            <path d="M12 20h9" />
                            <path d="M16.5 3.5a2.1 2.1 0 0 1 3 3L7 19l-4 1 1-4Z" />
                          </svg>
                        </Button>
                      )}
                    </p>
                    {editing && (
                      <div id={`mapping-v${v.verse}`}>
                        {mapping.isLoading && (
                          <p role="status" className="mt-3 font-sans text-sm text-fg-muted">
                            {t("bible.mapping.loading")}
                          </p>
                        )}
                        {mapping.isError && (
                          <div
                            role="alert"
                            className="mt-3 flex flex-wrap items-center gap-4 font-sans text-sm"
                          >
                            <p>{t("bible.mapping.loadError")}</p>
                            <Button variant="secondary" size="sm" onClick={() => mapping.refetch()}>
                              {t("bible.retry")}
                            </Button>
                            <Button variant="ghost" size="sm" onClick={closeEditor}>
                              {t("bible.mapping.actions.close")}
                            </Button>
                          </div>
                        )}
                        {mappingData && verseView === "original" && (
                          <OriginalVerse
                            data={mappingData}
                            reference={`${reference(ref)},${v.verse}`}
                            linkedIds={linked.original}
                            onHoverWord={setHoveredOriginalId}
                            showLemma={showLemma}
                            onToggleLemma={() => setShowLemma(!showLemma)}
                            showStrongs={showStrongs}
                            showMorphology={showMorphology}
                            onToggleStrongs={() => setShowStrongs(!showStrongs)}
                            onToggleMorphology={() => setShowMorphology(!showMorphology)}
                          />
                        )}
                        {mappingData && verseView === "original" && !showTsk && (
                          <EyeCrossReferences
                            references={referencesByVerse.get(v.verse)}
                            loading={crossReferences.isPending}
                            books={books}
                            preview={{
                              bookId: ref.bookId,
                              chapter: ref.chapter,
                              verse: v.verse,
                              level: reader.level,
                            }}
                          />
                        )}
                        {mappingData && verseView === "align" && (
                          <MappingEditor
                            data={mappingData}
                            review={reviewWords}
                            reference={`${reference(ref)},${v.verse}`}
                            saving={saveMapping.isPending}
                            saveError={saveError()}
                            saveErrorDetails={describeApiErrors(saveMapping.error)}
                            partsEditable={revisedId === undefined}
                            onAiRedo={
                              canRevise && revisedId === undefined
                                ? () => setAiRange({ fromVerse: v.verse, toVerse: v.verse })
                                : undefined
                            }
                            onCancel={closeEditor}
                            onSave={(links, { clearParts }) =>
                              revisedId !== undefined
                                ? saveRevisionMapping.mutate(
                                    { id: revisedId, links },
                                    { onSuccess: closeEditor },
                                  )
                                : saveBaseMapping.mutate(
                                    { ref: { ...ref, verse: v.verse }, links, clearParts },
                                    { onSuccess: closeEditor },
                                  )
                            }
                          />
                        )}
                      </div>
                    )}
                    {revising && (
                      <div id={`revision-v${v.verse}`}>
                        <RevisionEditor
                          verseRef={{ ...ref, verse: v.verse }}
                          reference={`${reference(ref)},${v.verse}`}
                          onClose={closeRevision}
                        />
                      </div>
                    )}
                  </div>
                  {wide && sideMode !== "off" && (
                    <VerseSide
                      mode={sideMode}
                      verse={v.verse}
                      bookId={ref.bookId}
                      chapter={ref.chapter}
                      level={reader.level}
                      reference={`${reference(ref)},${v.verse}`}
                      books={books}
                      references={referencesByVerse.get(v.verse)}
                      expanded={expandedVerse === v.verse}
                      onExpandedChange={onExpandedChange}
                      original={originalsByVerse.get(v.verse)}
                      language={originals.data?.language ?? "greek"}
                      lexicon={originals.data?.lexicon}
                      linkedIds={sideLinks.verse === v.verse ? sideLinks.original : NO_IDS}
                      onHoverOriginal={onHoverOriginal}
                    />
                  )}
                </div>
              );
            })}
        </article>
        {margin && selectedWord && selectedVerse && (
          <MarginStack verse={selectedVerse.verse} colRef={colRef}>
            {/* reserved height, so the pinned card does not jump when the hover info comes and goes */}
            <div className="min-h-24">{infoData && <WordInfoCard info={infoData} />}</div>
            {pinnedPopover}
          </MarginStack>
        )}
        {margin && infoData && infoVerse && !selectedWord && (
          <MarginStack verse={infoVerse.verse} colRef={colRef}>
            <WordInfoCard info={infoData} />
            {!selectedWord && hoveredWord && hoveredDetail.status === "success" && (
              <WordDetailPopover
                id={popoverId}
                word={hoveredWord}
                status={hoveredDetail.status}
                detail={hoveredDetail.data}
                books={books}
                onRetry={() => {}}
                onClose={() => {}}
                variant="margin"
                pinned={false}
              />
            )}
          </MarginStack>
        )}
      </div>
      {!margin && pinnedPopover}
      <footer className="reader-col mt-8 flex justify-between">
        {renderLink(prev, "previous", true)}
        {renderLink(next, "next", true)}
      </footer>
    </>
  );
}

/** Full TSK list of a verse under its original text in the eye panel (where the side panel does not show it). */
function EyeCrossReferences({
  references,
  loading,
  books,
  preview,
}: {
  references?: CrossReference[];
  loading: boolean;
  books?: BibleStructureBook[];
  preview: { bookId: number; chapter: number; verse: number; level: number };
}) {
  const { t } = useTranslation();
  if (loading || !references || references.length === 0) return null;
  return (
    <section aria-label={t("bible.sidePanel.eyeSection")} className="map-panel mt-2 font-sans">
      <h3 className="mb-2 text-sm font-medium text-fg-muted">{t("bible.sidePanel.eyeSection")}</h3>
      <CrossReferenceList
        variant="inline"
        references={references}
        books={books}
        preview={preview}
      />
    </section>
  );
}
