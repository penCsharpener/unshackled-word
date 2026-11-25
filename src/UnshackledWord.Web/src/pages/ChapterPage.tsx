import { useQuery, useQueryClient } from "@tanstack/react-query";
import {
  type DragEvent,
  Fragment,
  type MouseEvent as ReactMouseEvent,
  type PointerEvent as ReactPointerEvent,
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
import { CrossReferenceList, crossReferenceKey } from "@/components/CrossReferenceList";
import { HeaderPickerBar } from "@/components/HeaderPickerBar";
import { WordInfoCard } from "@/components/HeaderWordInfo";
import { LevelSlider } from "@/components/LevelSlider";
import { MappingEditor, type MappingSaveError } from "@/components/MappingEditor";
import { MarginStack } from "@/components/MarginStack";
import { NoteMarker } from "@/components/NoteMarker";
import { NotesOverlay, type NotesOverlayRequest, noteTarget } from "@/components/NotesOverlay";
import { OriginalVerse } from "@/components/OriginalVerse";
import { RevisionEditor } from "@/components/RevisionEditor";
import { type WordDetailCloseReason, WordDetailPopover } from "@/components/WordDetailPopover";
import { track, trackRevisionLevel } from "@/lib/analytics";
import { ApiError, describeApiError, describeApiErrors } from "@/lib/api";
import { useAuth } from "@/lib/auth/AuthContext";
import { hasWriteRole } from "@/lib/auth/roles";
import { adjacentChapter, bookName, chapterPath, parseChapterParams } from "@/lib/bible/books";
import { mainEditionCodes, useGreekEditionFilter } from "@/lib/bible/greekEditions";
import {
  chapterWordsQuery,
  useBibleStructure,
  useChapterWords,
  useGreekEditions,
  verseCrossReferenceTextsQuery,
} from "@/lib/bible/queries";
import {
  nextSidePanelMode,
  type SidePanelMode,
  useChapterCrossReferences,
  useChapterOriginals,
  useChapterSimilarVerses,
  useSidePanelMode,
  useTskOnlyHighlighted,
} from "@/lib/bible/sidePanel";
import { isProposal, useLinkTskWord, wordLinkRequest } from "@/lib/bible/tskCuration";
import { strongsFlashKeys } from "@/lib/bible/tskFlash";
import type {
  BibleStructureBook,
  ChapterRef,
  CrossReference,
  CrossReferenceWordLink,
  HighlightKind,
  SimilarVerseHit,
  WordToken,
} from "@/lib/bible/types";
import { useSaveVerseMapping, useVerseMapping } from "@/lib/bible/verseMapping";
import { parseVerseSelection } from "@/lib/bible/verseSelection";
import { useWordDetail, wordDetailQuery, wordDetailSource } from "@/lib/bible/wordDetail";
import { buildHighlights, phraseWordIds, wordKey } from "@/lib/bible/words";
import { useSetHeaderInfo } from "@/lib/headerInfo";
import { anchorFor, type Note, type NoteTarget, noteEndVerse, noteVerse } from "@/lib/notes/api";
import { useChapterNotes, useUpdateNote } from "@/lib/notes/queries";
import {
  isWordSelected,
  noteMarkerWord,
  selectionRange,
  selectionTarget,
  type WordPoint,
  type WordSelection,
} from "@/lib/notes/selection";
import { useShowOthersNotes } from "@/lib/notes/showOthers";
import { reviewMarkers, toMappingData } from "@/lib/revision/mapping";
import { useRevisionMapping, useSaveRevisionMapping } from "@/lib/revision/queries";
import { useReaderLevel } from "@/lib/revision/readerLevel";
import { useMediaQuery } from "@/lib/useMediaQuery";
import { SidePanelRail } from "./chapter/SidePanelRail";
import { type VerseLinkMode, VerseSide } from "./chapter/VerseSide";

const HIGHLIGHT_CLASSES: Record<HighlightKind, string> = {
  same: "bg-mark-same",
  related: "bg-mark-related",
};
/** Words translated together with the open word; an underline so it combines with the hover highlight. */
/** Hover/focus must rest this long on a word before its detail is prefetched. */
const PREFETCH_DELAY_MS = 200;
const NO_IDS: ReadonlySet<number> = new Set();
const NO_KEYS: ReadonlySet<string> = new Set();
const PHRASE_CLASSES = "underline decoration-accent decoration-2 underline-offset-4";
/** Words with TSK word links (roadmap 0024.5f, everyone): a subtle dotted underline in the link token. */
const TSK_LINK_CLASSES =
  "underline decoration-mark-link decoration-dotted decoration-2 underline-offset-4";
/** Touch: holding a word this long marks it as the first word of a note selection. */
const LONG_PRESS_MS = 500;
/** A touch that moves further than this (px) is a scroll, not a long press. */
const LONG_PRESS_SLOP = 10;
/** After a drag or a long press the click that follows must not open the word detail. */
const CLICK_SUPPRESS_MS = 400;

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
  const auth = useAuth();
  const canRevise = hasWriteRole(auth.user);
  const signedIn = auth.status === "authenticated";
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
  const showSimilar = wide && sideMode === "similar";
  const [storedEdition, setGreekEdition] = useGreekEditionFilter();
  const { data: editionCatalog } = useGreekEditions(showOriginal);
  const editionCodes = mainEditionCodes(editionCatalog);
  // a stored code the catalog does not know falls back to "Alle"; the filter never applies to the OT
  const hebrewChapter = ref !== null && ref.bookId < 40;
  const greekEdition =
    !hebrewChapter && storedEdition && editionCodes.includes(storedEdition) ? storedEdition : null;
  // The eye panel gets its own TSK list wherever the side panel does not show it.
  const eyeOriginalOpen = editingVerse !== undefined && verseView === "original";
  // TSK curation (roadmap 0024.3f): "Nur hervorgehobene" for everyone, curate mode with hidden links for curators.
  const [onlyHighlighted, setOnlyHighlighted] = useTskOnlyHighlighted();
  const [curateTsk, setCurateTsk] = useState(false);
  const [tskEditMode, setTskEditMode] = useState(false);
  const tskCurate = canRevise && curateTsk && showTsk;
  const crossReferences = useChapterCrossReferences(
    ref,
    showTsk || eyeOriginalOpen,
    tskCurate,
    reader.level,
  );
  const originals = useChapterOriginals(ref, showOriginal);
  // similar verses (roadmap 0027.3f): fetched only while that mode is shown
  const similar = useChapterSimilarVerses(ref, showSimilar);
  const similarByVerse = useMemo(
    () => new Map(similar.data?.verses.map((v) => [v.verse, v])),
    [similar.data],
  );
  const referencesByVerse = useMemo(
    () => new Map(crossReferences.data?.verses.map((v) => [v.verse, v.references])),
    [crossReferences.data],
  );
  /**
   * Words carrying TSK word links per verse (reader marker, roadmap 0024.5f): `shown` = positions in the shown verse
   * (from `mappedWords` on a revised verse; an empty list adds nothing = verse level), `base` = Elb1871 positions.
   */
  const tskLinkedWords = useMemo(() => {
    const byVerse = new Map<number, { shown: Set<number>; base: Set<number> }>();
    for (const v of crossReferences.data?.verses ?? []) {
      for (const r of v.references) {
        const w = r.word;
        if (r.rating === "hide" || w?.state !== "ok" || w.startPosition === undefined) continue;
        let entry = byVerse.get(v.verse);
        if (!entry) {
          entry = { shown: new Set(), base: new Set() };
          byVerse.set(v.verse, entry);
        }
        if (w.mappedWords) {
          for (const m of w.mappedWords) entry.shown.add(m.positionInVerse);
        } else {
          for (let p = w.startPosition; p <= (w.endPosition ?? w.startPosition); p++) {
            entry.base.add(p);
          }
        }
      }
    }
    return byVerse;
  }, [crossReferences.data]);
  /** Hovered TSK pill with a word link: its words are marked in the verse text (and the other way round below). */
  const [hoveredPill, setHoveredPill] = useState<{ verse: number; word: CrossReferenceWordLink }>();
  const onHoverReference = useCallback((verse: number, r: CrossReference | undefined) => {
    setHoveredPill(r?.word?.state === "ok" ? { verse, word: r.word } : undefined);
  }, []);
  /** Hovered similar-verse hit (roadmap 0027.4f): its Elb1871 source positions are marked in the verse text like a pill's words. */
  const [hoveredSimilar, setHoveredSimilar] = useState<{ verse: number; positions: number[] }>();
  const onHoverSimilar = useCallback((verse: number, hit: SimilarVerseHit | undefined) => {
    setHoveredSimilar(
      hit?.sourcePositions?.length ? { verse, positions: hit.sourcePositions } : undefined,
    );
  }, []);
  /** Positions of the words of the hovered pill or similar hit: shown positions (revised verse) and Elb1871 base positions. */
  const pillWords = useMemo(() => {
    if (hoveredSimilar) {
      return {
        verse: hoveredSimilar.verse,
        shown: new Set<number>(),
        base: new Set(hoveredSimilar.positions),
      };
    }
    const word = hoveredPill?.word;
    if (!hoveredPill || !word || word.startPosition === undefined) return undefined;
    const shown = new Set<number>();
    const base = new Set<number>();
    if (word.mappedWords) for (const m of word.mappedWords) shown.add(m.positionInVerse);
    else
      for (let p = word.startPosition; p <= (word.endPosition ?? word.startPosition); p++)
        base.add(p);
    return { verse: hoveredPill.verse, shown, base };
  }, [hoveredPill, hoveredSimilar]);
  // Target texts of the hovered verse (shared with the link hover card): their Strong's keys flash the pills too.
  const hoveredSenseKeys = useMemo(
    () => hoveredWordRaw?.senses.map((s) => s.key) ?? [],
    [hoveredWordRaw],
  );
  const flashTexts = useQuery({
    ...verseCrossReferenceTextsQuery(
      ref?.bookId ?? 0,
      ref?.chapter ?? 0,
      hoveredVerse?.verse ?? 0,
      reader.level,
    ),
    enabled:
      showTsk &&
      ref !== null &&
      !!hoveredVerse &&
      hoveredSenseKeys.length > 0 &&
      (referencesByVerse.get(hoveredVerse.verse)?.length ?? 0) > 0,
  });
  /** Pills whose word is hovered in the verse text, or whose target verses contain the hovered word's original word, flash. */
  const wordFlash = useMemo(() => {
    if (!showTsk || !hoveredVerse || !hoveredWordRaw) return undefined;
    const keys = strongsFlashKeys(hoveredSenseKeys, flashTexts.data?.references, crossReferenceKey);
    for (const r of referencesByVerse.get(hoveredVerse.verse) ?? []) {
      const w = r.word;
      if (r.rating === "hide" || w?.state !== "ok" || w.startPosition === undefined) continue;
      const hit = w.mappedWords
        ? hoveredWordRaw.position !== undefined &&
          w.mappedWords.some((m) => m.positionInVerse === hoveredWordRaw.position)
        : hoveredWordRaw.basePosition !== undefined &&
          hoveredWordRaw.basePosition >= w.startPosition &&
          hoveredWordRaw.basePosition <= (w.endPosition ?? w.startPosition);
      if (hit) keys.add(crossReferenceKey(r));
    }
    return keys.size > 0 ? { verse: hoveredVerse.verse, keys } : undefined;
  }, [showTsk, hoveredVerse, hoveredWordRaw, hoveredSenseKeys, flashTexts.data, referencesByVerse]);
  const originalsByVerse = useMemo(
    () => new Map(originals.data?.verses.map((v) => [v.verse, v])),
    [originals.data],
  );
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
            : onlyHighlighted &&
                !crossReferences.data.verses.some((v) =>
                  v.references.some((r) => r.rating === "highlight"),
                )
              ? "emptyHighlighted"
              : "idle"
      : sideMode === "original"
        ? originals.isError
          ? "error"
          : originals.isPending
            ? "loading"
            : "idle"
        : sideMode === "similar"
          ? similar.isError
            ? "error"
            : similar.isPending
              ? "loading"
              : similar.data.verses.every((v) => v.hits.length === 0)
                ? "empty"
                : "idle"
          : "idle";

  // Notes (roadmap 0023): markers at verse numbers, words and the heading open the notes overlay (`NotesOverlay`,
  // also `N`). MDXEditor is only fetched once a note is shown or written (lazy chunks inside the overlay).
  const { showOthers, setShowOthers } = useShowOthersNotes();
  const notes = useChapterNotes(books ? ref : null, reader.level, showOthers);
  /** Words notes sit on their last shown word; verse, range, drifted and unmapped notes at the verse number. */
  const noteMarkers = useMemo(() => {
    const byVerse = new Map<number | "chapter", Note[]>();
    const byWord = new Map<string, Note[]>();
    const ranges: { start: number; end: number; own: boolean }[] = [];
    const shown = new Map((verses.data ?? []).map((v) => [v.verse, v]));
    for (const note of notes.data ?? []) {
      const verse = noteVerse(note);
      const word = verse === undefined ? undefined : noteMarkerWord(note, shown.get(verse));
      if (word !== undefined) {
        byWord.set(word, [...(byWord.get(word) ?? []), note]);
        continue;
      }
      const key = verse ?? "chapter";
      byVerse.set(key, [...(byVerse.get(key) ?? []), note]);
      const end = noteEndVerse(note);
      if (verse !== undefined && end !== undefined && end > verse) {
        ranges.push({ start: verse, end, own: note.isOwn });
      }
    }
    return { byVerse, byWord, ranges };
  }, [notes.data, verses.data]);
  const [notesRequest, setNotesRequest] = useState<NotesOverlayRequest>();
  /** last verse note action, so a shift-click on a second verse's action makes it a range */
  const lastVerseDraft = useRef<number>(undefined);
  /** The overlay at a note: its passage is the marker's anchor (verse markers: the verse). */
  function openNote(id: string, passage?: NoteTarget) {
    const note = notes.data?.find((n) => n.id === id);
    setNotesRequest({
      passage: passage ?? (note ? noteTarget(note) : { kind: "chapter" }),
      noteId: id,
    });
  }
  /** The overlay in write mode for a new note at `target`. */
  function startNote(target: NoteTarget) {
    setNotesRequest({
      passage: target,
      write: true,
      filter: target.kind === "chapter" ? "chapter" : "passage",
    });
  }
  const renderMarker = (
    list: Note[] | undefined,
    passage: NoteTarget | undefined,
    label: (count: number, first: Note) => string,
  ) => {
    const first = list?.[0];
    if (!list || !first) return null;
    return (
      <NoteMarker
        label={label(list.length, first)}
        count={list.length}
        own={list.some((n) => n.isOwn)}
        onClick={() => openNote(first.id, passage)}
      />
    );
  };
  const markerFor = (key: number | "chapter", label: (count: number) => string) =>
    renderMarker(
      noteMarkers.byVerse.get(key),
      key === "chapter" ? { kind: "chapter" } : { kind: "verse", verse: key },
      label,
    );

  function startVerseNote(verse: number, extend: boolean) {
    // shift-click on a second verse's note action turns the last verse note action into a range
    const from = lastVerseDraft.current;
    lastVerseDraft.current = verse;
    if (extend && from !== undefined && from !== verse) {
      startNote({ kind: "verses", verse: Math.min(from, verse), endVerse: Math.max(from, verse) });
      return;
    }
    startNote({ kind: "verse", verse });
  }

  // Word selection for notes (roadmap 0023.2f): mouse drag or click, shift-click; touch: long-press, then tap the last word.
  const [selection, setSelection] = useState<WordSelection>();
  const [dragging, setDragging] = useState(false);
  /** Own drifted note being re-anchored: the next selection's action moves it there. */
  const [reanchor, setReanchor] = useState<Note>();
  const updateNote = useUpdateNote();
  const dragFrom = useRef<WordPoint>(undefined);
  const dragged = useRef(false);
  const lastClicked = useRef<WordPoint>(undefined);
  /** a drag swallows the next word click, a long press only the click on the pressed word */
  const suppressClick = useRef<{ until: number; point?: WordPoint }>({ until: 0 });
  const longPress = useRef<{
    timer?: ReturnType<typeof setTimeout>;
    x: number;
    y: number;
    /** the word whose long press marked it */
    fired?: WordPoint;
    /** the last press on a word was a touch */
    touch: boolean;
  }>({ x: 0, y: 0, touch: false });
  useEffect(() => {
    const press = longPress.current;
    function onUp() {
      clearTimeout(press.timer);
      if (dragged.current || press.fired) {
        suppressClick.current = { until: Date.now() + CLICK_SUPPRESS_MS, point: press.fired };
      }
      press.fired = undefined;
      dragFrom.current = undefined;
      dragged.current = false;
      setDragging(false);
    }
    window.addEventListener("pointerup", onUp);
    window.addEventListener("pointercancel", onUp);
    return () => {
      window.removeEventListener("pointerup", onUp);
      window.removeEventListener("pointercancel", onUp);
      clearTimeout(press.timer);
    };
  }, []);
  // a press outside the words and the selection action clears the selection
  useEffect(() => {
    if (!selection) return;
    function onDown(event: PointerEvent) {
      if (
        event.target instanceof Element &&
        event.target.closest("[data-word-trigger], [data-note-selection]")
      ) {
        return;
      }
      setSelection(undefined);
    }
    window.addEventListener("pointerdown", onDown);
    return () => window.removeEventListener("pointerdown", onDown);
  }, [selection]);

  // Link mode (roadmap 0024.5f, every logged-in user since 0024.6f, TSK side panel): refs of one verse are selected in the panel and
  // dropped (drag) or placed (tap / Enter) on a word of that verse. The notes selection is off meanwhile.
  const [linkModeOn, setLinkModeOn] = useState(false);
  /** Ctrl held (desktop): link mode meanwhile, so Ctrl-clicking pills selects several without the switch. */
  const [ctrlHeld, setCtrlHeld] = useState(false);
  const [linkSelection, setLinkSelection] = useState<{ verse: number; keys: string[] }>();
  /** The last placement was sent as a suggestion (202, non-maintainers): the hint bar confirms it. */
  const [linkProposed, setLinkProposed] = useState(false);
  // A selection (or the confirmation) keeps link mode on after Ctrl is released, until it is placed or cancelled.
  const linkMode =
    signedIn && showTsk && (linkModeOn || ctrlHeld || linkSelection !== undefined || linkProposed);
  const trackCtrl = signedIn && showTsk;
  useEffect(() => {
    if (!trackCtrl) {
      setCtrlHeld(false);
      return;
    }
    const down = (event: KeyboardEvent) => event.key === "Control" && setCtrlHeld(true);
    const up = (event: KeyboardEvent) => event.key === "Control" && setCtrlHeld(false);
    const release = () => setCtrlHeld(false);
    window.addEventListener("keydown", down);
    window.addEventListener("keyup", up);
    window.addEventListener("blur", release);
    return () => {
      window.removeEventListener("keydown", down);
      window.removeEventListener("keyup", up);
      window.removeEventListener("blur", release);
    };
  }, [trackCtrl]);
  const [linkDrag, setLinkDrag] = useState<{ verse: number; references: CrossReference[] }>();
  const [dropHover, setDropHover] = useState<WordPoint>();
  /** Last word a link was placed on: Shift on the next drop extends to a range from it. */
  const lastDrop = useRef<{ verse: number; position: number }>(undefined);
  const linkWord = useLinkTskWord();
  useEffect(() => {
    if (linkMode) {
      setSelection(undefined);
      return;
    }
    setLinkSelection(undefined);
    setLinkProposed(false);
    setLinkDrag(undefined);
    setDropHover(undefined);
  }, [linkMode]);
  const linkSelected = useMemo(() => new Set(linkSelection?.keys), [linkSelection]);
  const onLinkSelectedChange = useCallback((verse: number, keys: string[]) => {
    setLinkSelection(keys.length > 0 ? { verse, keys } : undefined);
    setLinkProposed(false);
  }, []);
  const onLinkDragStart = useCallback((verse: number, references: CrossReference[]) => {
    setLinkDrag({ verse, references });
  }, []);
  const onLinkDragEnd = useCallback(() => {
    setLinkDrag(undefined);
    setDropHover(undefined);
  }, []);
  const verseLink = useMemo<VerseLinkMode>(
    () => ({
      dragging: linkDrag !== undefined,
      onSelectedChange: onLinkSelectedChange,
      onDragStart: onLinkDragStart,
      onDragEnd: onLinkDragEnd,
    }),
    [linkDrag, onLinkSelectedChange, onLinkDragStart, onLinkDragEnd],
  );
  /** The verse whose words take the dragged or selected refs; words of other verses are no targets. */
  const linkVerse = linkMode ? (linkDrag?.verse ?? linkSelection?.verse) : undefined;
  function linkTo(verse: number, references: CrossReference[], position: number, extend: boolean) {
    if (!ref || references.length === 0) return;
    const last = lastDrop.current;
    const from = extend && last?.verse === verse ? last.position : position;
    if (!extend || last?.verse !== verse) lastDrop.current = { verse, position };
    linkWord.mutate(
      wordLinkRequest(
        { bookId: ref.bookId, chapter: ref.chapter, verse },
        references,
        from,
        position,
      ),
      {
        onSuccess: (result) => {
          setLinkSelection(undefined);
          setLinkProposed(isProposal(result));
        },
      },
    );
  }
  /** The selected refs of the selection verse, in list order. */
  function selectedRefs() {
    if (!linkSelection) return [];
    const keys = new Set(linkSelection.keys);
    return (referencesByVerse.get(linkSelection.verse) ?? []).filter((r) =>
      keys.has(crossReferenceKey(r)),
    );
  }

  function wordPointerDown(event: ReactPointerEvent, point: WordPoint) {
    // link mode: no note selection (drag, long press), the words are drop targets
    if (linkMode) return;
    longPress.current.touch = event.pointerType === "touch";
    if (event.pointerType === "touch") {
      const press = longPress.current;
      clearTimeout(press.timer);
      press.x = event.clientX;
      press.y = event.clientY;
      press.fired = undefined;
      press.timer = setTimeout(() => {
        press.fired = point;
        setSelectedKey(undefined);
        setSelection({ anchor: point, focus: point, pending: true });
      }, LONG_PRESS_MS);
      return;
    }
    if (event.button !== 0 || event.shiftKey) return;
    dragFrom.current = point;
    dragged.current = false;
  }
  function wordPointerMove(event: ReactPointerEvent) {
    if (event.pointerType !== "touch") return;
    const press = longPress.current;
    const moved = Math.abs(event.clientX - press.x) + Math.abs(event.clientY - press.y);
    if (moved > LONG_PRESS_SLOP) clearTimeout(press.timer);
  }
  function wordPointerEnter(event: ReactPointerEvent, point: WordPoint) {
    const from = dragFrom.current;
    if (linkMode) return;
    if (!from || event.pointerType === "touch" || (event.buttons & 1) === 0) return;
    if (!dragged.current && from.verse === point.verse && from.index === point.index) return;
    if (!dragged.current) {
      dragged.current = true;
      setDragging(true);
      setSelectedKey(undefined);
    }
    window.getSelection()?.removeAllRanges();
    setSelection({ anchor: from, focus: point });
  }
  function wordClick(event: ReactMouseEvent<HTMLElement>, point: WordPoint, word: WordToken) {
    const suppress = suppressClick.current;
    if (
      Date.now() < suppress.until &&
      (!suppress.point ||
        (suppress.point.verse === point.verse && suppress.point.index === point.index))
    ) {
      return;
    }
    if (linkMode) {
      // tap / Enter with a selection places it on a word of its verse; without one the word detail opens
      if (linkSelection) {
        if (point.verse === linkSelection.verse && word.basePosition !== undefined) {
          linkTo(point.verse, selectedRefs(), word.basePosition, event.shiftKey);
        }
        return;
      }
      toggle(word, event.currentTarget);
      return;
    }
    if (selection?.pending) {
      setSelection({ anchor: selection.anchor, focus: point });
      return;
    }
    const from = lastClicked.current;
    if (event.shiftKey && from) {
      setSelectedKey(undefined);
      setSelection({ anchor: from, focus: point });
      return;
    }
    lastClicked.current = point;
    if (reanchor) {
      setSelection({ anchor: point, focus: point });
      return;
    }
    setSelection(undefined);
    toggle(word, event.currentTarget);
  }

  const selected = useMemo(() => {
    if (!selection || !verses.data) return undefined;
    const target = selectionTarget(selection, verses.data);
    return target && { target, end: selectionRange(selection).end };
  }, [selection, verses.data]);
  function actOnSelection(target: NoteTarget) {
    if (!ref) return;
    const note = reanchor;
    if (note) {
      updateNote.mutate(
        {
          id: note.id,
          bodyMarkdown: note.bodyMarkdown,
          anchor: anchorFor(ref.bookId, ref.chapter, target),
        },
        {
          onSuccess: () => {
            setReanchor(undefined);
            setSelection(undefined);
            openNote(note.id);
          },
        },
      );
      return;
    }
    setSelection(undefined);
    startNote(target);
  }
  function startReanchor(note: Note) {
    updateNote.reset();
    setSelection(undefined);
    setReanchor(note);
    setNotesRequest(undefined);
  }

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
      const checkbox = event.target instanceof HTMLInputElement && event.target.type === "checkbox";
      if (isTypingTarget(event.target) && !checkbox) return;
      if (event.target instanceof Element && event.target.closest("[data-mapping-editor]")) return;
      // a modal (notes overlay, search, dialogs) owns the keyboard
      if (document.querySelector("dialog[open]")) return;
      if (event.key === "Escape" && linkSelection) {
        setLinkSelection(undefined);
        return;
      }
      if (event.key === "Escape" && (selection || reanchor)) {
        if (selection) setSelection(undefined);
        else setReanchor(undefined);
        return;
      }
      if (event.key === "g" && wide) {
        changeSideMode(nextSidePanelMode(sideMode));
        return;
      }
      // N: notes of the selected words, else of the chapter
      if (event.key === "n") {
        event.preventDefault();
        const target = selected?.target;
        setNotesRequest(
          target ? { passage: target } : { passage: { kind: "chapter" }, filter: "chapter" },
        );
        return;
      }
      if (selectedKey !== undefined) return;
      if (event.key === "ArrowLeft" && prevPath) navigate(prevPath);
      if (event.key === "ArrowRight" && nextPath) navigate(nextPath);
    }
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, [
    prevPath,
    nextPath,
    navigate,
    selectedKey,
    wide,
    sideMode,
    changeSideMode,
    selection,
    selected,
    reanchor,
    linkSelection,
  ]);

  const bookId = ref?.bookId;
  const chapterNumber = ref?.chapter;
  // biome-ignore lint/correctness/useExhaustiveDependencies: closing on every chapter change is the point
  useEffect(() => {
    setSelectedKey(undefined);
    setEditingVerse(undefined);
    setReviseVerse(undefined);
    setNotesRequest(undefined);
    lastVerseDraft.current = undefined;
    setSelection(undefined);
    setReanchor(undefined);
    lastClicked.current = undefined;
    setLinkSelection(undefined);
    lastDrop.current = undefined;
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

  // `?note=<id>` (link from "Meine Notizen") opens the notes overlay at that note once per chapter visit; the hash scrolls to its verse
  const noteParam = new URLSearchParams(location.search).get("note");
  const openedNoteKey = useRef<string | undefined>(undefined);
  const noteLoaded = notes.isSuccess;
  const noteKnown = noteParam !== null && (notes.data ?? []).some((n) => n.id === noteParam);
  // biome-ignore lint/correctness/useExhaustiveDependencies: openNote only sets local state
  useEffect(() => {
    if (!noteParam || !noteLoaded || !noteKnown) return;
    const key = `${bookId}:${chapterNumber}:${noteParam}`;
    if (openedNoteKey.current === key) return;
    openedNoteKey.current = key;
    openNote(noteParam);
  }, [noteParam, noteLoaded, noteKnown, bookId, chapterNumber]);

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
        <div
          className="sticky top-(--reader-sticky-top) z-10 bg-bg pt-2 pb-2"
          data-testid="reader-sticky-head"
        >
          <header className="flex items-center justify-between gap-4">
            {renderLink(prev, "previous", false)}
            <div className="flex items-start gap-1">
              <h1 className="text-center font-scripture text-3xl font-semibold md:text-4xl">
                {reference(ref)}
              </h1>
              {markerFor("chapter", (count) =>
                t("notes.markerChapter", { count, reference: reference(ref) }),
              )}
            </div>
            {renderLink(next, "next", false)}
          </header>
          <div className="mt-2 flex flex-wrap justify-center gap-2">
            {canRevise && (
              <Button variant="ghost" size="sm" onClick={() => setAiRange({ fromVerse: 1 })}>
                {t("agent.chapter.aiRedo")}
              </Button>
            )}
            <Button variant="ghost" size="sm" onClick={() => startNote({ kind: "chapter" })}>
              {t("notes.addChapter")}
            </Button>
          </div>
          {linkMode &&
            (linkSelection || linkWord.isPending || linkWord.isError || linkProposed) && (
              // overlay under the sticky header: appearing or changing never moves the verses
              <div
                role="status"
                data-testid="tsk-link-hint"
                className="absolute inset-x-0 top-full mt-1 flex flex-wrap items-center gap-2 rounded-md border border-mark-link bg-surface p-2 font-sans text-sm"
              >
                {linkSelection && (
                  <span>
                    {t("bible.tskLink.selectionHint", {
                      count: linkSelection.keys.length,
                      verse: linkSelection.verse,
                    })}
                  </span>
                )}
                {linkProposed && !linkSelection && !linkWord.isPending && (
                  <span data-testid="tsk-link-proposed">
                    {t("bible.tskCuration.proposal.sent")}
                  </span>
                )}
                {linkWord.isPending && (
                  <span className="text-fg-muted">{t("bible.tskLink.linking")}</span>
                )}
                {linkWord.isError && (
                  <span role="alert">
                    {describeApiError(linkWord.error, t("bible.tskLink.linkError"))}
                  </span>
                )}
                <Button
                  variant="ghost"
                  size="sm"
                  onClick={() => {
                    setLinkSelection(undefined);
                    setLinkProposed(false);
                    linkWord.reset();
                  }}
                >
                  {linkProposed && !linkSelection
                    ? t("bible.tskCuration.proposal.close")
                    : t("bible.tskLink.cancel")}
                </Button>
              </div>
            )}
        </div>
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
            edition={{
              value: greekEdition,
              codes: editionCodes,
              onChange: setGreekEdition,
              disabled: hebrewChapter,
            }}
            onRetry={() =>
              void (sideMode === "tsk"
                ? crossReferences.refetch()
                : sideMode === "similar"
                  ? similar.refetch()
                  : originals.refetch())
            }
            tsk={{
              onlyHighlighted,
              onOnlyHighlightedChange: setOnlyHighlighted,
              edit: signedIn ? { checked: tskEditMode, onChange: setTskEditMode } : undefined,
              curate: canRevise ? { checked: curateTsk, onChange: setCurateTsk } : undefined,
              link: signedIn
                ? {
                    // shows the effective state: on while Ctrl is held or a selection is pending
                    checked: linkMode,
                    onChange: (on: boolean) => {
                      setLinkModeOn(on);
                      if (!on) {
                        setLinkSelection(undefined);
                        setLinkProposed(false);
                      }
                    },
                  }
                : undefined,
            }}
          />
        )}
        {reanchor && (
          <div
            role="status"
            className="mt-4 flex flex-wrap items-center gap-2 rounded-md border border-accent bg-surface p-2 font-sans text-sm"
          >
            <span>
              {t("notes.reanchorPrompt", {
                anchor: `${reference(ref)},${noteVerse(reanchor) ?? ""}`,
              })}
            </span>
            {updateNote.isError && (
              <span role="alert">
                {describeApiError(updateNote.error, t("notes.reanchorError"))}
              </span>
            )}
            <Button variant="ghost" size="sm" onClick={() => setReanchor(undefined)}>
              {t("notes.cancel")}
            </Button>
          </div>
        )}
        <article
          lang="de"
          aria-busy={verses.isLoading}
          className={`mt-6 font-scripture text-lg leading-relaxed md:text-xl laptop:text-2xl ${dragging ? "select-none" : ""}`}
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
              const bracket = noteMarkers.ranges.find(
                (r) => r.start <= v.verse && v.verse <= r.end,
              );
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
                  {bracket && (
                    <span
                      aria-hidden="true"
                      data-note-bracket={
                        v.verse === bracket.start
                          ? "start"
                          : v.verse === bracket.end
                            ? "end"
                            : "mid"
                      }
                      className={`pointer-events-none absolute -left-6 hidden w-1.5 border-l-2 laptop:block ${bracket.own ? "border-accent" : "border-note-other"} ${v.verse === bracket.start ? "top-1 rounded-tl-sm border-t-2" : "top-0"} ${v.verse === bracket.end ? "bottom-1 rounded-bl-sm border-b-2" : "-bottom-2"}`}
                    />
                  )}
                  <div className="verse-main">
                    <p>
                      <sup className="mr-1 text-xs text-fg-muted">{v.verse}</sup>
                      {markerFor(v.verse, (count) =>
                        t("notes.markerVerse", { count, verse: v.verse }),
                      )}
                      {v.words.map((word, index) => {
                        const key = wordKey(word);
                        const hasDetail = hasDetailId(word);
                        const linkId = word.revisionWordId ?? word.id;
                        const interactive = word.senses.length > 0 && hasDetail;
                        const kind = highlights.get(key);
                        const point = { verse: v.verse, index };
                        const marked = isWordSelected(selection, point);
                        const dropTarget = linkVerse === v.verse && word.basePosition !== undefined;
                        const dropHere =
                          dropTarget && dropHover?.verse === v.verse && dropHover.index === index;
                        const tskLinked = tskLinkedWords.get(v.verse);
                        const carriesLink =
                          tskLinked !== undefined &&
                          ((word.position !== undefined && tskLinked.shown.has(word.position)) ||
                            (word.basePosition !== undefined &&
                              tskLinked.base.has(word.basePosition)));
                        const pillHover =
                          pillWords?.verse === v.verse &&
                          ((word.position !== undefined && pillWords.shown.has(word.position)) ||
                            (word.basePosition !== undefined &&
                              pillWords.base.has(word.basePosition)));
                        const background = dropHere
                          ? "bg-mark-link"
                          : marked
                            ? "bg-mark-note"
                            : pillHover ||
                                (hasDetail && linked.german.has(linkId)) ||
                                (v.verse === sideLinks.verse &&
                                  word.revisionWordId === undefined &&
                                  sideLinks.german.has(word.id))
                              ? "bg-mark-linked"
                              : kind
                                ? HIGHLIGHT_CLASSES[kind]
                                : "";
                        const actionHere =
                          selected &&
                          selected.end.verse === v.verse &&
                          selected.end.index === index;
                        return (
                          <Fragment key={key}>
                            <Button
                              variant="inline"
                              data-word-trigger=""
                              data-selected={marked ? "" : undefined}
                              aria-haspopup={hasDetail ? "dialog" : undefined}
                              aria-expanded={hasDetail ? selectedKey === key : undefined}
                              aria-controls={selectedKey === key ? popoverId : undefined}
                              aria-label={t("bible.wordDetail.trigger", { word: word.text })}
                              onClick={(e) => wordClick(e, point, word)}
                              onPointerDown={(e) => wordPointerDown(e, point)}
                              onPointerMove={wordPointerMove}
                              onPointerEnter={(e) => wordPointerEnter(e, point)}
                              {...(dropTarget && {
                                "data-link-drop": "",
                                onDragOver: (e: DragEvent<HTMLButtonElement>) => {
                                  if (!linkDrag) return;
                                  e.preventDefault();
                                  try {
                                    e.dataTransfer.dropEffect = "link";
                                  } catch {
                                    // no data transfer (tests)
                                  }
                                  if (!dropHere) setDropHover(point);
                                },
                                onDragLeave: () =>
                                  setDropHover((current) =>
                                    current?.verse === v.verse && current.index === index
                                      ? undefined
                                      : current,
                                  ),
                                onDrop: (e: DragEvent<HTMLButtonElement>) => {
                                  e.preventDefault();
                                  const dragged = linkDrag?.references ?? [];
                                  setLinkDrag(undefined);
                                  setDropHover(undefined);
                                  if (word.basePosition !== undefined) {
                                    linkTo(v.verse, dragged, word.basePosition, e.shiftKey);
                                  }
                                },
                              })}
                              data-tsk-link={carriesLink ? "" : undefined}
                              onContextMenu={(e) => {
                                // a long press marks the word instead of opening the system menu
                                if (longPress.current.touch) e.preventDefault();
                              }}
                              onMouseEnter={
                                interactive ? () => startIntent(word, v.words) : undefined
                              }
                              onMouseLeave={interactive ? endIntent : undefined}
                              onFocus={interactive ? () => startIntent(word, v.words) : undefined}
                              onBlur={interactive ? endIntent : undefined}
                              className={`${background} ${hasDetail && phrase.has(linkId) ? PHRASE_CLASSES : carriesLink ? TSK_LINK_CLASSES : ""} ${selectedKey === key ? "word-anchor" : ""}`}
                            >
                              {word.text}
                            </Button>
                            {renderMarker(noteMarkers.byWord.get(key), undefined, (count, first) =>
                              t("notes.markerWords", {
                                count,
                                quote: first.anchor.quotedText ?? word.text,
                              }),
                            )}
                            {actionHere && (
                              <span data-note-selection="" className="relative">
                                <span className="absolute start-0 top-full z-20 mt-1 flex items-center gap-1 whitespace-nowrap rounded-md border border-border bg-surface p-1 font-sans text-sm leading-normal shadow-lg">
                                  <Button
                                    variant="primary"
                                    size="sm"
                                    onClick={() => actOnSelection(selected.target)}
                                  >
                                    {reanchor
                                      ? t("notes.reanchorHere")
                                      : selected.target.kind === "verses"
                                        ? t("notes.selectionVerses", {
                                            from: selected.target.verse,
                                            to: selected.target.endVerse,
                                          })
                                        : selected.target.kind === "verse"
                                          ? t("notes.selectionVerse", {
                                              verse: selected.target.verse,
                                            })
                                          : t("notes.selectionWords")}
                                  </Button>
                                  {selection?.pending && (
                                    <span className="px-1 text-xs text-fg-muted">
                                      {t("notes.selectionPending")}
                                    </span>
                                  )}
                                  <Button
                                    variant="ghost"
                                    size="sm"
                                    aria-label={t("notes.selectionCancel")}
                                    title={t("notes.selectionCancel")}
                                    onClick={() => setSelection(undefined)}
                                  >
                                    <span aria-hidden="true">×</span>
                                  </Button>
                                </span>
                              </span>
                            )}{" "}
                          </Fragment>
                        );
                      })}
                      <Button
                        variant="ghost"
                        size="sm"
                        className="map-trigger"
                        aria-label={t("notes.addVerse", { verse: v.verse })}
                        title={t("notes.addVerse", { verse: v.verse })}
                        onClick={(e) => startVerseNote(v.verse, e.shiftKey)}
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
                          <path d="M14.5 3H6a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8.5Z" />
                          <path d="M14 3v6h6M12 12v6M9 15h6" />
                        </svg>
                      </Button>
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
                  {wide && sideMode !== "off" && (sideMode !== "similar" || similar.data) && (
                    <VerseSide
                      mode={sideMode}
                      verse={v.verse}
                      bookId={ref.bookId}
                      chapter={ref.chapter}
                      level={reader.level}
                      reference={`${reference(ref)},${v.verse}`}
                      books={books}
                      references={referencesByVerse.get(v.verse)}
                      onlyHighlighted={onlyHighlighted}
                      showHidden={tskCurate}
                      editable={signedIn && tskEditMode}
                      suggest={signedIn && !canRevise}
                      link={linkMode ? verseLink : undefined}
                      linkSelected={linkSelection?.verse === v.verse ? linkSelected : undefined}
                      flashKeys={wordFlash?.verse === v.verse ? wordFlash.keys : NO_KEYS}
                      onHoverReference={onHoverReference}
                      original={originalsByVerse.get(v.verse)}
                      language={originals.data?.language ?? "greek"}
                      lexicon={originals.data?.lexicon}
                      linkedIds={sideLinks.verse === v.verse ? sideLinks.original : NO_IDS}
                      onHoverOriginal={onHoverOriginal}
                      edition={greekEdition}
                      similar={similarByVerse.get(v.verse)}
                      onHoverSimilar={onHoverSimilar}
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
      <NotesOverlay
        request={notesRequest}
        onClose={() => setNotesRequest(undefined)}
        bookId={ref.bookId}
        chapter={ref.chapter}
        chapterLabel={reference(ref)}
        verses={verses.data}
        notes={notes.data ?? []}
        status={notes.status}
        onRetry={() => void notes.refetch()}
        signedIn={signedIn}
        showOthers={showOthers}
        onShowOthersChange={setShowOthers}
        onReanchor={signedIn ? startReanchor : undefined}
      />
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
