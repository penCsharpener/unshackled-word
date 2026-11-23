export type Testament = "ot" | "nt";

export interface BibleStructureBook {
  id: number;
  testament: Testament;
  nameDe: string;
  nameEn: string;
  abbreviationDe: string;
  abbreviationEn: string;
  /** index 0 = chapter 1; Heb (= Elb1871) versification */
  versesPerChapter: number[];
}
export interface GetBibleStructureResponse {
  books: BibleStructureBook[];
}

export interface ChapterVerse {
  verse: number;
  text: string;
}
/** A verse shown in its revised wording at the requested level. */
export interface RevisedVerse {
  verse: number;
  level: number;
  revisionVerseId: number;
}
export interface GetChapterTextResponse {
  bibleBookId: number;
  chapter: number;
  verses: ChapterVerse[];
  revisedVerses?: RevisedVerse[];
}

/** One row of `chapterWords`. Nulls are omitted by the API, hence optional. */
export interface ChapterWord {
  id: number;
  verse: number;
  positionInVerse: number;
  wordInContext: string;
  plainWord?: string;
  lemma?: string;
  original?: string;
  originalLemma?: string;
  strongs?: string;
  verseCode?: string;
  grammaticalKey?: string;
  /** Words of revised verses only. `id` is then the base word id, or 0 for a word without base word. */
  revisionWordId?: number;
  /** Words of revised verses only: the Elb1871 position the word is based on (omitted for a word without base word). */
  basePositionInVerse?: number;
}
export interface GetWordsOfChapterResponse {
  bibleBookId: number;
  chapter: number;
  words: ChapterWord[];
  revisedVerses?: RevisedVerse[];
}

export interface ChapterRef {
  bookId: number;
  chapter: number;
}
export interface PickerGroup {
  key: string;
  bookId: number;
  title: string;
  chapters: number[];
}

/** One original-language reading of an Elberfelder word: shared `key` (Strong's, else lemma) plus its surface form. */
export interface WordSense {
  key: string;
  form: string;
}
/** An Elberfelder word with all its original-language readings (the API returns one row per reading). */
export interface WordToken {
  /** Base word id; 0 for a revision word without base word (so never unique, use `wordKey`). */
  id: number;
  text: string;
  senses: WordSense[];
  revisionWordId?: number;
  /** Position in the shown verse (Elb1871 or variant). */
  position?: number;
  /** Elb1871 `PositionInVerse` (base words: `position`; variant words: their base position, none for a variant-only word). Note anchors use it. */
  basePosition?: number;
}
export interface VerseWords {
  verse: number;
  words: WordToken[];
  /** Set when the verse is shown in its revised wording. */
  revised?: RevisedVerse;
}
/** `same`: shares a key and the same original form; `related`: shares a key with a different form. */
export type HighlightKind = "same" | "related";

export type OriginalLanguage = "greek" | "hebrew" | "aramaic";

export type PartOfSpeechKey =
  | "adjective"
  | "adverb"
  | "article"
  | "conjunction"
  | "indeclinable"
  | "interjection"
  | "noun"
  | "nounProper"
  | "nounProperPerson"
  | "nounProperPlace"
  | "particle"
  | "preposition"
  | "pronoun"
  | "suffix"
  | "verb";
export type TenseKey =
  | "present"
  | "imperfect"
  | "future"
  | "aorist"
  | "perfect"
  | "pluperfect"
  | "futurePerfect";
export type VoiceKey = "active" | "middle" | "passive" | "middleOrPassive";
export type MoodKey =
  | "indicative"
  | "subjunctive"
  | "optative"
  | "imperative"
  | "infinitive"
  | "participle";
export type PersonKey = "first" | "second" | "third";
export type CaseKey = "nominative" | "genitive" | "dative" | "accusative" | "vocative";
export type NumberKey = "singular" | "dual" | "plural";
export type GenderKey = "masculine" | "feminine" | "neuter" | "common";
export type DegreeKey = "comparative" | "superlative";
export type HebrewStemKey =
  | "qal"
  | "niphal"
  | "piel"
  | "pual"
  | "hiphil"
  | "hophal"
  | "hithpael"
  | "other";
export type HebrewFormKey =
  | "perfect"
  | "imperfect"
  | "sequentialPerfect"
  | "sequentialImperfect"
  | "cohortative"
  | "jussive"
  | "imperative"
  | "infinitiveAbsolute"
  | "infinitiveConstruct"
  | "participleActive"
  | "participlePassive";
export type HebrewStateKey = "absolute" | "construct" | "determined";

/** A morphology code plus its decoded parts. Parts are stable keys, translated by i18n (`bible.grammar.*`). */
export interface Morphology {
  code: string; // "N-NSM", "V-IAI-3S", "HR/Ncfsa", "HNcmpa"
  partOfSpeech?: PartOfSpeechKey;
  tense?: TenseKey; // Greek
  voice?: VoiceKey; // Greek
  mood?: MoodKey; // Greek
  person?: PersonKey;
  case?: CaseKey; // Greek
  number?: NumberKey;
  gender?: GenderKey;
  degree?: DegreeKey; // Greek
  stem?: HebrewStemKey; // Hebrew/Aramaic verbs
  hebrewForm?: HebrewFormKey; // Hebrew/Aramaic verbs
  state?: HebrewStateKey; // Hebrew/Aramaic nominals
  /** English STEP description, only when the decoder could not map the code. */
  description?: string;
}

/** One part of a Hebrew/Aramaic word (prefix, root, suffix), in word order. */
export interface Morpheme {
  role: "prefix" | "root" | "suffix";
  original: string;
  strongs?: string; // "H9003"
  gloss?: string; // English
  morphology?: Morphology;
}

export interface LexiconSummary {
  strongs: string;
  lemma: string;
  transliteration?: string;
  gloss?: string; // English
  definition?: string; // English, markup stripped, max ~600 chars
  definitionTruncated?: boolean;
  /** `definition` cut into plain and reference pieces (roadmap 0019); concatenated texts equal `definition`. */
  definitionSegments?: DefinitionSegment[];
}

/** A piece of lexicon text; `ref` marks a recognised scripture reference (Hebrew counting, whole chapter = verse 0 to 999). */
export interface DefinitionSegment {
  text: string;
  ref?: { bookId: number; path: string; spans: ParsedSpan[] };
}

export interface ChapterLexiconEntry {
  gloss?: string;
  definition?: string;
  definitionSegments?: DefinitionSegment[];
}

/** One original-language word behind the German word. */
export interface Rendering {
  word: string;
  count: number;
}

/** A person or place of the STEP lists behind a proper noun, matched by Strong's number. */
export interface NamedEntity {
  kind: "person" | "place";
  name: string;
  brief: string; // English, one line
}

export interface OriginalReading {
  stepWordId: number;
  language: OriginalLanguage;
  positionInVerse: number; // position in the original verse; readings are sorted by it
  germanWordPart?: string; // part of the German word this reading covers
  original: string; // with diacritics
  transliteration?: string;
  lemma?: string;
  strongs: string; // STEP disambiguated root, e.g. "G3056", "H7225G"
  gloss?: string; // dictionary gloss, English
  contextGloss?: string; // meaning in this verse, English
  morphology?: Morphology; // whole word (Hebrew: root part)
  morphemes?: Morpheme[]; // Hebrew/Aramaic only
  lexicon?: LexiconSummary;
  /** uses of this root Strong's number in its testament (NT for Greek, OT for Hebrew/Aramaic) */
  occurrenceCount?: number;
  /** most used Elberfelder renderings of this Strong's number, most used first (top 5) */
  renderings?: Rendering[];
  /** other German words of the verse mapped to this same original word (ids from `coMappedWords`) */
  sharedWithWordIds?: number[];
  /** set when the Strong's number belongs to a STEP person or place */
  entity?: NamedEntity;
}

/** Who wrote the current version of the word's mapping rows. */
/** `carried`, `manual` and `seed` come from revision variants (roadmap 0011). */
export type MappingSource = "ai" | "human" | "carried" | "manual" | "seed";
/** Provenance of the word's mapping (Elb1871GreekMapping / Elb1871HebrewMapping); omitted when unmapped. */
export interface MappingProvenance {
  source: MappingSource;
  createdAt: string; // ISO 8601, current version
  model?: string; // AI model id, AI only, e.g. "gemini-3.1-flash-lite-preview"
  confirmedAt?: string; // ISO 8601, a human confirmed the AI mapping
}
/** Derived for the badge: AI unconfirmed, AI confirmed by a human, or written by a human. */
export type MappingStatus = "aiSuggested" | "confirmed" | "manual" | "carried";

/**
 * `sameOriginal`: shares at least one original word ("Und die Erde" → וְהָאָרֶץ);
 * `addedWord`: added by the translator for this word (ParentGermanWordId = this word);
 * `parent`: the word this added word belongs to.
 */
export type CoMappedRelation = "sameOriginal" | "addedWord" | "parent";
export interface CoMappedWord {
  wordId: number;
  wordInContext: string;
  positionInVerse: number;
  relation: CoMappedRelation;
}
/** Server-derived uncertainty hints (there is no confidence value in the data). */
export type MappingHintKey = "manyOriginals" | "unclearWordPart" | "textVariants";

/** TSK target in Elberfelder numbering; `endVerse` makes it a range inside the same chapter. */
export interface CrossReference {
  bibleBookId: number;
  chapter: number;
  verse: number;
  endVerse?: number;
  /** English KJV keyword of the TSK entry; only in the chapter response, not shown. */
  keyword?: string;
  /**
   * Curation (roadmap 0024.3): `highlight` (listed first) or `hide` (only in the curators' chapter view with
   * `includeHidden`); omitted when neutral.
   */
  rating?: TskRating;
  /** Curator comment, plain text (max 500 characters); omitted when there is none. */
  comment?: string;
  /** `added`: a link curators added (roadmap 0024.4), listed after the TSK links; omitted for TSK links. */
  origin?: "added";
  /** Word link (roadmap 0024.5): the source-verse word(s) this link belongs to; omitted for verse-level links. */
  word?: CrossReferenceWordLink;
  /**
   * Chapter list only, roadmap 0024.6: the caller's own pending suggestion for this link (full proposed state; no
   * `rating` = neutral proposed). Only the author sees it; a proposed added link comes with `origin: "added"`.
   */
  pendingProposal?: TskPendingProposal;
}

/** The caller's pending suggestion on a chapter cross reference (`TskPendingProposalResponse`). */
export interface TskPendingProposal {
  id: number;
  rating?: TskRating;
  comment?: string;
  word?: CrossReferenceWordLink;
  /** The added link is proposed for removal. */
  delete?: true;
}

/** Proposed or current state of one link in a suggestion (nulls omitted: no `rating` = neutral). */
export interface TskProposedState {
  origin: "tsk" | "added";
  rating?: TskRating;
  comment?: string;
  word?: CrossReferenceWordLink;
  delete?: true;
}

export type TskProposalState = "pending" | "accepted" | "rejected" | "superseded";

/** A user's suggestion (`TskProposal`, roadmap 0024.6). */
export interface TskProposal {
  id: number;
  sourceHebRefId: number;
  targetStartHebRefId: number;
  targetEndHebRefId?: number;
  state: TskProposalState;
  proposed: TskProposedState;
  createdAt: string;
  decidedAt?: string;
  reason?: string;
}

/** 202 answer of `PUT tsk/curation`, `POST`/`DELETE tsk/curation/added` for non-maintainers. */
export interface TskProposalSubmitted {
  proposal: TskProposal;
}

/** 202 answer of `PUT`/`DELETE tsk/curation/word` for non-maintainers (keys already in that state left out). */
export interface TskProposalsSubmitted {
  proposals: TskProposal[];
}

/** `GET tsk/proposals/mine`: the caller's suggestions, newest first, all states. */
export interface MyTskProposalsResponse {
  proposals: TskProposal[];
}

/** One pending suggestion on `GET maintainer/tsk/proposals`. */
export interface TskReviewProposal {
  id: number;
  sourceHebRefId: number;
  targetStartHebRefId: number;
  targetEndHebRefId?: number;
  inTsk: boolean;
  /** No curation row yet: omitted. */
  current?: TskProposedState & { updatedAt: string };
  proposed: TskProposedState;
  authorUserId: string;
  authorDisplayName: string;
  createdAt: string;
  /** The link changed since the suggestion was made. */
  stale: boolean;
}

export interface TskReviewProposalsResponse {
  proposals: TskReviewProposal[];
}

/** `POST maintainer/tsk/proposals/{id}/accept`. */
export interface AcceptTskProposalResponse {
  proposal: TskProposal;
  curation: TskCurationResponse;
}

/**
 * Word link of a TSK link (roadmap 0024.5). Positions are Elb1871 `PositionInVerse` of the source verse; `drifted`
 * (the words there changed since linking) omits them and keeps the quote, the link then sits at verse level.
 */
export interface CrossReferenceWordLink {
  startPosition?: number;
  endPosition?: number;
  quotedText: string;
  state: "ok" | "drifted";
  /**
   * Chapter list with `?level=` only, ok links on a verse with a variant at that level: the variant words whose
   * base position is in the range; an empty list = the variant has no such word (show at verse level).
   */
  mappedWords?: { positionInVerse: number; basePositionInVerse: number }[];
}

/** Key of one link of a source verse (Elberfelder HebRefIds; end only for a range). */
export interface TskLinkKey {
  targetStartHebRefId: number;
  targetEndHebRefId?: number;
}

/** `PUT tsk/curation/word`: links 1..100 links of one source verse to the words `startPosition..endPosition`. */
export interface SaveTskWordLinkRequest {
  sourceHebRefId: number;
  keys: TskLinkKey[];
  startPosition: number;
  endPosition: number;
}

/** `DELETE tsk/curation/word`: removes the word link of these links (body). */
export interface DeleteTskWordLinkRequest {
  sourceHebRefId: number;
  keys: TskLinkKey[];
}

/** Answer of `PUT`/`DELETE tsk/curation/word`: the state of every link, in request order. */
export interface TskWordLinkResponse {
  links: TskCurationResponse[];
}

/** Curation rating of one TSK link; neutral is `null` in a request and omitted in a response. */
export type TskRating = "highlight" | "hide";

/** `PUT tsk/curation`: the full rating/comment state of one link, keyed in Elberfelder numbering (HebRefIds). */
export interface SaveTskCurationRequest {
  sourceHebRefId: number;
  targetStartHebRefId: number;
  /** Omitted for a single target verse. */
  targetEndHebRefId?: number;
  rating: TskRating | null;
  /** Trimmed; `null` removes the comment. */
  comment: string | null;
}

/** `POST tsk/curation/added`: a new link from a source verse to a German reference (e.g. "Röm 8,28-30"). */
export interface AddTskCurationRequest {
  sourceHebRefId: number;
  reference: string;
  /** Trimmed; `null` for none (max 500). */
  comment: string | null;
}

/** `DELETE tsk/curation/added`: the key of an added link (body, like the PUT). */
export interface DeleteAddedTskCurationRequest {
  sourceHebRefId: number;
  targetStartHebRefId: number;
  /** Omitted for a single target verse. */
  targetEndHebRefId?: number;
}

/** State of the link after a save; a link back at plain TSK has no row (only key and `origin`). */
export interface TskCurationResponse {
  sourceHebRefId: number;
  targetStartHebRefId: number;
  targetEndHebRefId?: number;
  origin: "tsk" | "added";
  rating?: TskRating;
  comment?: string;
  /** Word link as stored (no drift check on write answers). */
  word?: { startPosition: number; endPosition: number; quotedText: string; state: "ok" };
  updatedAt?: string;
}

/** `GET elberfelder/crossReferenceTexts/...`: the texts of all TSK targets of one verse, in TSK order. */
export interface GetVerseCrossReferenceTextsResponse {
  references: (CrossReference & { verses: CrossReferenceVerseText[] })[];
}

/** Original text of one verse: plain surface words joined (roadmap 0024.1). */
export interface VerseOriginalText {
  language: "hebrew" | "aramaic" | "greek";
  text: string;
}

/** German text of one target verse; `original` is omitted where the verse has no original words. */
export interface CrossReferenceVerseText {
  verse: number;
  text: string;
  original?: VerseOriginalText;
}

/** `GET elberfelder/chapterCrossReferences/...`: verses without TSK references are omitted. */
export interface GetChapterCrossReferencesResponse {
  bibleBookId: number;
  chapter: number;
  verses: { verse: number; references: CrossReference[] }[];
}

/** `GET elberfelder/chapterOriginals/...`: original words of a whole chapter with the Elb1871 links (read only). */
export interface ChapterOriginalVerse {
  verse: number;
  words: VerseMappingOriginalWord[];
  links: { germanId: number; originalId: number }[];
}
export interface GetChapterOriginalsResponse {
  bibleBookId: number;
  chapter: number;
  language: "greek" | "hebrew";
  verses: ChapterOriginalVerse[];
  /** English gloss and shortened lexicon text per Strong's number of the chapter */
  lexicon?: Record<string, ChapterLexiconEntry>;
}

export interface RevisionWordInfo {
  level: number;
  change: "same" | "changed" | "new";
  /** the Elb1871 word a `changed` word replaces */
  baseWord?: string;
}
export interface GetWordDetailResponse {
  wordId: number;
  wordInContext: string;
  positionInVerse: number;
  germanLemma?: string;
  isAddedWord?: boolean; // omitted when false
  readings: OriginalReading[]; // [] = not linked (or added word)
  mapping?: MappingProvenance;
  /** Only for words of a revised verse: what the word is relative to Elb1871. */
  revision?: RevisionWordInfo;
  coMappedWords?: CoMappedWord[]; // same verse, sorted by positionInVerse
  hints?: MappingHintKey[];
  /** TSK cross references of the word's verse (verse level, same list for every word of the verse), TSK order, max 20. */
  crossReferences?: CrossReference[];
  /** omitted when false: the verse has more cross references than listed */
  crossReferencesTruncated?: boolean;
}

export interface StrongsOccurrence {
  bibleBookId: number;
  chapter: number;
  verse: number;
  original?: string;
  /** The whole original verse in verse order; the word of this row is `matched`. */
  originalVerse?: StrongsOccurrenceToken[];
  before: StrongsOccurrenceToken[];
  keyword: StrongsOccurrenceToken[];
  /** Punctuation that trailed the keyword; drawn outside the keyword box. */
  keywordSuffix?: string;
  after: StrongsOccurrenceToken[];
}
export interface StrongsOccurrenceToken {
  text: string;
  matched: boolean;
}
export interface GetStrongsOccurrencesResponse {
  strongs: string;
  language: "greek" | "hebrew";
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
  items: StrongsOccurrence[];
}

/** Optional filters of the Strong's concordance; `rendering` is a `StrongsRendering.key`. */
export interface StrongsOccurrenceFilters {
  /** Elberfelder book id 1-66. */
  book?: number;
  rendering?: string;
}

export interface StrongsBookStat {
  bibleBookId: number;
  count: number;
  verseCount: number;
}
export interface StrongsRendering {
  /** Display spelling. */
  word: string;
  /** Filter value for `strongsOccurrences?rendering=`. */
  key: string;
  /** Part of speech (snapshot mode only). */
  pos?: string;
  count: number;
  /** Instances linked but without an own German lemma (contractions); `word` and `key` are empty. */
  noOwnLemma?: boolean;
}
/** One lexicon variant, only present when the number was requested without a letter (G3004 = G3004G + G3004H). */
export interface StrongsVariant {
  strongs: string;
  lemma: string;
  transliteration?: string;
  gloss?: string;
  count: number;
}
export interface GetStrongsSummaryResponse {
  strongs: string;
  language: OriginalLanguage;
  lemma?: string;
  transliteration?: string;
  /** English STEP gloss. */
  gloss?: string;
  occurrenceCount: number;
  verseCount: number;
  mappedCount: number;
  unmappedCount: number;
  byBook: StrongsBookStat[];
  renderings: StrongsRendering[];
  variants?: StrongsVariant[];
  /** When the stats snapshot was computed; absent while the live fallback answers. */
  statsRefreshedAt?: string | null;
  /** True while a refresh runs on the server. */
  statsRefreshing: boolean;
  /** When the link-role backfill ran; absent = counts still include auxiliaries and prefixes. */
  linkRolesAppliedAt?: string | null;
}

export type WordStatsTestament = "all" | "ot" | "nt";
export interface WordStatsOriginal {
  strongs: string;
  language: OriginalLanguage;
  lemma?: string;
  transliteration?: string;
  gloss?: string;
  /** Occurrences of this Strong's number rendered with the word. */
  count: number;
  /** All occurrences of the Strong's number. */
  totalCount: number;
}
export interface WordStatsCandidate {
  lemma: string;
  pos?: string;
  occurrences: number;
}
export interface GetWordStatsResponse {
  /** Snapshot mode: the resolved lemma display. */
  word: string;
  lemma?: string;
  pos?: string;
  /** All lemma/PoS matches of the typed word, most frequent first (the answering one included). */
  candidates?: WordStatsCandidate[];
  statsRefreshedAt?: string | null;
  testament: WordStatsTestament;
  occurrenceCount: number;
  verseCount: number;
  unmappedCount: number;
  byBook: StrongsBookStat[];
  originals: WordStatsOriginal[];
}

export interface VerseMappingGermanWord {
  id: number;
  positionInVerse: number;
  text: string;
}
export interface VerseMappingOriginalWord {
  id: number;
  position: number;
  original: string;
  transliteration?: string;
  lemma?: string; // dictionary form
  strongs?: string;
  gloss?: string; // English
  morphologyCode?: string;
  /** decoded `morphologyCode`; chapter originals and verse mapping responses */
  morphology?: Morphology;
  /** Hebrew only (verse mapping): the morphemes of the word in order (וַיֹּאמֶר = H9001 + H0559), punctuation left out. */
  morphemes?: VerseMappingMorpheme[];
}
/** One morpheme of a Hebrew word (roadmap 0018); a link picks one with `strongsNumber`. */
export interface VerseMappingMorpheme {
  /** Strong's tag without braces (`H9001`, `H0430G`) */
  strongs: string;
  /** the braced main morpheme; a link without `strongsNumber` renders this one */
  isMain: boolean;
  original?: string;
  gloss?: string;
}
/** `head` carries the meaning of the source morpheme (counted); `unit` belongs to the head's German form (`habe` in `habe getauft`). */
export type MappingRole = "head" | "unit";
/**
 * One mapping row of a verse. A normal link has `stepWordId`; an added word has `isAddedWord`
 * and `parentElbWordId` (the German word it belongs to) instead.
 */
export interface VerseMappingLink {
  elbWordId: number;
  stepWordId?: number;
  isAddedWord: boolean;
  parentElbWordId?: number;
  /** Compound word (`Gerstenernte` = `Gersten` + `ernte`): 1-based order of the part, set together with `germanWordPart`. */
  partOrder?: number;
  /** The part of the German word this link belongs to (`ernte`); every link of a word with parts has one. */
  germanWordPart?: string;
  /**
   * Roadmap 0018. Always set in responses; in a save, null means head. Sending any of `role`, `strongsNumber`, `partLemma`,
   * `allowMultiHead` makes the save role-aware (all invariants checked); see `stateToLinks`.
   */
  role?: MappingRole;
  /** Hebrew only: the morpheme of the original word this link renders; null = the main morpheme. */
  strongsNumber?: string;
  /** Lemma of the compound part (`Gerste` of `Gersten`); only with `germanWordPart`. */
  partLemma?: string;
  /** Several heads accepted on this link's morpheme (idioms); returned true on each of its heads. */
  allowMultiHead?: boolean;
}
export interface GetVerseMappingResponse {
  bibleBookId: number;
  chapter: number;
  verse: number;
  language: "greek" | "hebrew";
  /** false: the editor is read-only (no Save) */
  editingEnabled: boolean;
  germanWords: VerseMappingGermanWord[];
  originalWords: VerseMappingOriginalWord[];
  links: VerseMappingLink[];
}
export interface PutVerseMappingRequest {
  links: VerseMappingLink[];
  /** Confirms that stored compound parts of the verse may be removed by this save. */
  clearParts?: boolean;
}

/** Smart-search lookup (`search/originals`), see roadmap 0008.5. */
export interface OriginalsLookupOriginal {
  strongs: string;
  language: OriginalLanguage;
  lemma?: string;
  gloss?: string;
  count: number;
  share: number;
  totalCount: number;
}
export interface OriginalsLookupLemma {
  lemma: string;
  word: string;
  pos?: string;
  occurrences: number;
  lemmaOccurrences: number;
  highFrequency: boolean;
  originals: OriginalsLookupOriginal[];
}
export interface OriginalsLookupToken {
  token: string;
  lemmas: OriginalsLookupLemma[];
}
export interface OriginalsLookupResponse {
  statsRefreshedAt: string;
  tokens: OriginalsLookupToken[];
}
export interface OriginalsLookupOptions {
  top?: number;
  includeFrequent?: boolean;
}

/** Smart-search lookup of Hebrew / Greek words (`search/sourcewords`), roadmap 0019. */
export interface SourceWordRef {
  bibleBookId: number;
  chapter: number;
  verse: number;
}
export interface SourceWordForm {
  /** The form as printed, with diacritics. */
  form: string;
  occurrences: number;
  refs: SourceWordRef[];
}
export interface SourceWordLemma {
  /** "G1577", "H430". */
  strongs: string;
  language: Exclude<OriginalLanguage, "aramaic">;
  lemma: string;
  transliteration: string;
  gloss: string;
  occurrences: number;
  matchedBy: "strongs" | "script" | "translit" | "skeleton" | "gloss" | "form";
  forms: SourceWordForm[];
}
export interface SourceWordsToken {
  token: string;
  lemmas: SourceWordLemma[];
}
export interface SourceWordsResponse {
  tokens: SourceWordsToken[];
}

export interface VerseSearchToken {
  text: string;
  matched: boolean;
}
export interface VerseSearchHit {
  bibleBookId: number;
  chapter: number;
  verse: number;
  tokens: VerseSearchToken[];
  /** True when the tokens are the revised wording at `level` (omitted for the Elb1871 text). */
  revised?: boolean;
  level?: number;
}
export interface VerseSearchQueryToken {
  text: string;
  lexemes: string[];
  /** Only present when the word was widened by lemma ("ging" also finds forms of "gehen"). */
  lemmas?: string[];
}
export interface VerseSearchBookCount {
  bookId: number;
  count: number;
}
export interface SearchVersesResponse {
  query: string;
  total: number;
  page: number;
  pageSize: number;
  tokens: VerseSearchQueryToken[];
  /** Hits per book for the whole query, independent of the book filter and of paging. */
  bookCounts: VerseSearchBookCount[];
  hits: VerseSearchHit[];
}
export interface SearchVersesOptions {
  page?: number;
  pageSize?: number;
  /** Restricts the hits to one book (the counts per book stay unfiltered). */
  bookId?: number | null;
  /** Reader level (default 100 = Elb1871 only): verses also match by the revision that level shows. */
  level?: number;
}

/** `GET api/references/parse` (roadmap 0019): what the search field input means as scripture references. */
interface ParsedReferenceGroup {
  display: string;
  path?: string;
}

interface ParsedSpan {
  startChapter: number;
  startVerse: number;
  endChapter: number;
  endVerse: number;
}

export interface ParsedReference {
  bookId?: number;
  bookOnly: boolean;
  display: string;
  path?: string;
  /** Verse ranges in Hebrew counting; a whole chapter is verse 0 to 999. */
  spans?: ParsedSpan[];
  groups: ParsedReferenceGroup[];
}

export interface ParseReferenceError {
  code: string;
  position: number;
  args: Record<string, string>;
}

export interface ParseReferenceResponse {
  references: ParsedReference[];
  error?: ParseReferenceError;
  bookCandidates: number[];
}

/** `GET passageText`: the verses of each requested span, in request order. */
export interface GetPassageTextResponse {
  passages: {
    bookId: number;
    chapter: number;
    verse: number;
    endVerse?: number;
    verses: ChapterVerse[];
  }[];
  /** More than 10 spans or 40 verses were asked for; the rest is left out. */
  truncated: boolean;
}
