import { useQuery } from "@tanstack/react-query";
import {
  type KeyboardEvent,
  type ReactNode,
  useEffect,
  useId,
  useLayoutEffect,
  useRef,
  useState,
} from "react";
import { useTranslation } from "react-i18next";
import { Link, useNavigate } from "react-router";
import { ApiError, describeApiError } from "@/lib/api";
import { bookAbbreviation, bookName } from "@/lib/bible/books";
import { structureQuery } from "@/lib/bible/queries";
import {
  firstReferencePath,
  replaceTypedBook,
  useReferenceParse,
  viewOfParse,
} from "@/lib/bible/referenceParse";
import { splitSourceWords, useSourceWordsLookup } from "@/lib/bible/sourceWords";
import { slotStrongsLink } from "@/lib/bible/strongsGrammar";
import {
  isLookupQuery,
  originalTextAttributes,
  useOriginalsLookup,
} from "@/lib/bible/strongsStats";
import type {
  BibleStructureBook,
  OriginalLanguage,
  OriginalsLookupLemma,
  OriginalsLookupOriginal,
  SourceWordForm,
  SourceWordLemma,
} from "@/lib/bible/types";
import {
  isVerseSearchQuery,
  originalsQueryOf,
  useVerseSearch,
  VERSE_SEARCH_PAGE_SIZE,
} from "@/lib/bible/verseSearch";
import { useReaderLevel } from "@/lib/revision/readerLevel";
import {
  applySuggestion,
  facetRequest,
  type GrammarFacetRequest,
  type GrammarSuggestion,
  type GrammarVocabulary,
  type GrammarVocabularyKey,
  type GrammarVocabularyValue,
  languageLimit,
  lemmaLookupWord,
  lemmaSuggestions,
  tokenAtCaret,
  vocabularySuggestions,
} from "@/lib/search/grammarQuery";
import {
  foldGrammar,
  formatGrammarFilter,
  formatGrammarWords,
  type GrammarHit,
  type GrammarProblem,
  type GrammarSlot,
  germanMatchText,
  germanReading,
  isGrammarQuery,
  languageOfOriginal,
  languageOfStrongs,
  parseGrammarProblem,
  replaceGrammarToken,
  toVerseHit,
  useGrammarFacets,
  useGrammarSearch,
  useGrammarVocabulary,
} from "@/lib/search/grammarSearch";
import {
  setSearchMark,
  setSearchQuery,
  setSearchTab,
  useSearchState,
} from "@/lib/search/searchState";
import { useSubmittedQuery } from "@/lib/search/useSubmittedQuery";
import { useTrackSearch } from "@/lib/search/useTrackSearch";
import { useCoarsePointer } from "@/lib/useCoarsePointer";
import { useDebouncedValue } from "@/lib/useDebouncedValue";
import { BookBarChart } from "./BookBarChart";
import { Button } from "./Button";
import { Dialog } from "./Dialog";
import { DonutChart, type DonutSlice } from "./DonutChart";
import { EmptyState } from "./EmptyState";
import { ReferenceCard } from "./ReferenceCard";
import type { SuggestionItem } from "./SuggestionList";
import { Switch } from "./Switch";
import { TextField } from "./TextField";
import { hitOptionId, hitPath, VerseHitList } from "./VerseHitList";

export interface SearchOverlayProps {
  open: boolean;
  onClose: () => void;
}

const normalized = (input: string) => input.trim().replace(/\s+/g, " ");

function OriginalText({ language, children }: { language: OriginalLanguage; children: ReactNode }) {
  const { lang, dir } = originalTextAttributes(language);
  return (
    <span lang={lang} dir={dir} className="font-original">
      {children}
    </span>
  );
}

function originalSlice(original: OriginalsLookupOriginal): DonutSlice {
  const name = [original.strongs, original.lemma].filter(Boolean).join(" ");
  return {
    key: original.strongs,
    text: original.gloss ? `${name} – ${original.gloss}` : name,
    value: original.count,
    label: (
      <>
        <span className="font-medium">{original.strongs}</span>
        {original.lemma ? (
          <>
            {" "}
            <OriginalText language={original.language}>{original.lemma}</OriginalText>
          </>
        ) : null}
        {original.gloss ? <span className="text-fg-muted"> – {original.gloss}</span> : null}
      </>
    ),
  };
}

/** One German lemma of a query word with its originals; very frequent words wait for a click. */
function LemmaBlock({
  typed,
  lemma,
  onNavigate,
}: {
  typed: string;
  lemma: OriginalsLookupLemma;
  onNavigate: () => void;
}) {
  const { t } = useTranslation();
  const [revealed, setRevealed] = useState(false);
  // Refetches only this word with includeFrequent=true; an empty query keeps the hook idle.
  const full = useOriginalsLookup(revealed ? typed : "", { includeFrequent: true });
  const shown = revealed
    ? (full.data?.tokens
        .flatMap((token) => token.lemmas)
        .find((l) => l.lemma === lemma.lemma && l.pos === lemma.pos) ?? lemma)
    : lemma;

  const same = typed.toLowerCase() === lemma.lemma.toLowerCase();
  const title = `${same ? lemma.lemma : t("bible.stats.donut.resolved", { typed, lemma: lemma.lemma })}${
    lemma.pos ? `, ${t(`bible.pos.${lemma.pos}`, { defaultValue: lemma.pos })}` : ""
  }`;

  if (shown.highFrequency && shown.originals.length === 0) {
    return (
      <EmptyState title={<span lang="de">{title}</span>}>
        <div className="grid justify-items-start gap-2">
          <p>{t("search.originals.frequent")}</p>
          {revealed && full.isError ? <p role="alert">{t("search.originals.error")}</p> : null}
          <Button
            variant="secondary"
            size="sm"
            disabled={revealed && full.isFetching}
            onClick={() => (revealed ? full.refetch() : setRevealed(true))}
          >
            {revealed && full.isFetching
              ? t("search.originals.loading")
              : t("search.originals.showAnyway")}
          </Button>
        </div>
      </EmptyState>
    );
  }
  if (shown.originals.length === 0) return null;
  return (
    <DonutChart
      title={<span lang="de">{title}</span>}
      subtitle={t("search.originals.total", { count: shown.occurrences })}
      slices={shown.originals.map(originalSlice)}
      centerLabel={t("search.originals.centerLabel")}
      sliceHref={(strongs) => `/bible/strongs/${encodeURIComponent(strongs)}`}
      onSliceNavigate={onNavigate}
    />
  );
}

const linkClasses = "text-accent underline";

/** One inflected form with its occurrences and a few verses it stands in. */
function SourceFormItem({
  form,
  language,
  books,
  appLanguage,
  onNavigate,
}: {
  form: SourceWordForm;
  language: OriginalLanguage;
  books: BibleStructureBook[];
  appLanguage: string;
  onNavigate: () => void;
}) {
  const { t } = useTranslation();
  return (
    <li className="flex flex-wrap items-baseline gap-x-2 text-sm">
      <span className="text-base">
        <OriginalText language={language}>{form.form}</OriginalText>
      </span>
      <span className="text-fg-muted">
        {t("search.sourceWords.times", { count: form.occurrences })}
      </span>
      {form.refs.map((ref) => {
        const book = books.find((b) => b.id === ref.bibleBookId);
        const label = `${book ? bookAbbreviation(book, appLanguage) : ref.bibleBookId} ${ref.chapter},${ref.verse}`;
        return (
          <Link
            key={`${ref.bibleBookId}:${ref.chapter}:${ref.verse}`}
            to={`/bible/${ref.bibleBookId}/${ref.chapter}#v${ref.verse}`}
            className={linkClasses}
            onClick={onNavigate}
          >
            {label}
          </Link>
        );
      })}
    </li>
  );
}

/** One Hebrew / Greek lemma of the source word lookup: Strong's link, lemma in the original font, gloss, occurrences, matching forms. */
function SourceLemmaCard({
  lemma,
  books,
  appLanguage,
  onNavigate,
}: {
  lemma: SourceWordLemma;
  books: BibleStructureBook[];
  appLanguage: string;
  onNavigate: () => void;
}) {
  const { t } = useTranslation();
  const headingId = useId();
  return (
    <article
      aria-labelledby={headingId}
      className="grid content-start gap-2 rounded-lg border border-border bg-surface p-4"
    >
      <h4 id={headingId} className="flex flex-wrap items-baseline gap-x-2 font-semibold">
        <Link
          to={`/bible/strongs/${encodeURIComponent(lemma.strongs)}`}
          className={linkClasses}
          onClick={onNavigate}
        >
          {lemma.strongs}
        </Link>
        <span className="font-scripture text-lg">
          <OriginalText language={lemma.language}>{lemma.lemma}</OriginalText>
        </span>
        {lemma.transliteration ? (
          <span className="text-sm font-normal text-fg-muted">{lemma.transliteration}</span>
        ) : null}
      </h4>
      {lemma.gloss ? <p className="text-sm">{lemma.gloss}</p> : null}
      <p className="text-sm text-fg-muted">
        {t("search.originals.total", { count: lemma.occurrences })}
      </p>
      {lemma.forms.length > 0 ? (
        <ul aria-label={t("search.sourceWords.forms")} className="grid gap-1">
          {lemma.forms.map((form) => (
            <SourceFormItem
              key={form.form}
              form={form}
              language={lemma.language}
              books={books}
              appLanguage={appLanguage}
              onNavigate={onNavigate}
            />
          ))}
        </ul>
      ) : null}
    </article>
  );
}

/** Hebrew / Greek words and Strong's numbers of the query: the lemmas they name or inflect from, with the matching forms. */
function SourceWordsSection({
  words,
  books,
  appLanguage,
  onNavigate,
}: {
  words: string;
  books: BibleStructureBook[];
  appLanguage: string;
  onNavigate: () => void;
}) {
  const { t } = useTranslation();
  const lookup = useSourceWordsLookup(words);
  useTrackSearch(
    "originals",
    `source|${words}`,
    lookup.data
      ? lookup.data.tokens.reduce((sum, token) => sum + token.lemmas.length, 0)
      : undefined,
  );

  if (lookup.isError) {
    if (lookup.error instanceof ApiError && lookup.error.status === 503) {
      return (
        <EmptyState title={t("search.originals.notReady")}>
          {t("search.originals.notReadyHint")}
        </EmptyState>
      );
    }
    return (
      <EmptyState>
        <div role="alert" className="grid justify-items-start gap-2">
          <p>{describeApiError(lookup.error, t("search.originals.error"))}</p>
          <Button variant="secondary" size="sm" onClick={() => lookup.refetch()}>
            {t("search.retry")}
          </Button>
        </div>
      </EmptyState>
    );
  }
  if (!lookup.data) return <EmptyState>{t("search.originals.loading")}</EmptyState>;

  const lemmas = lookup.data.tokens.flatMap((token) =>
    token.lemmas.map((lemma) => ({ token: token.token, lemma })),
  );
  if (lemmas.length === 0) return <EmptyState>{t("search.sourceWords.none")}</EmptyState>;
  return (
    <div className="grid gap-4">
      {lemmas.map(({ token, lemma }) => (
        <SourceLemmaCard
          key={`${token}|${lemma.strongs}`}
          lemma={lemma}
          books={books}
          appLanguage={appLanguage}
          onNavigate={onNavigate}
        />
      ))}
    </div>
  );
}

/** German query words: per word the Greek / Hebrew words it is mapped to, from the stats snapshot. */
function GermanOriginals({ words, onNavigate }: { words: string; onNavigate: () => void }) {
  const { t } = useTranslation();
  const lookup = useOriginalsLookup(words);
  useTrackSearch(
    "originals",
    `german|${words}`,
    lookup.data ? lookup.data.tokens.filter((token) => token.lemmas.length > 0).length : undefined,
  );

  if (lookup.isError) {
    if (lookup.error instanceof ApiError && lookup.error.status === 503) {
      return (
        <EmptyState title={t("search.originals.notReady")}>
          {t("search.originals.notReadyHint")}
        </EmptyState>
      );
    }
    return (
      <EmptyState>
        <div role="alert" className="grid justify-items-start gap-2">
          <p>{describeApiError(lookup.error, t("search.originals.error"))}</p>
          <Button variant="secondary" size="sm" onClick={() => lookup.refetch()}>
            {t("search.retry")}
          </Button>
        </div>
      </EmptyState>
    );
  }
  if (!lookup.data) return <EmptyState>{t("search.originals.loading")}</EmptyState>;

  const tokens = lookup.data.tokens.filter((token) => token.lemmas.length > 0);
  if (tokens.length === 0) return <EmptyState>{t("search.originals.none")}</EmptyState>;
  return (
    <div className="grid gap-4">
      {tokens.flatMap((token) =>
        token.lemmas.map((lemma) => (
          <LemmaBlock
            key={`${token.token}|${lemma.lemma}|${lemma.pos ?? ""}`}
            typed={token.token}
            lemma={lemma}
            onNavigate={onNavigate}
          />
        )),
      )}
    </div>
  );
}

/**
 * The originals column. A query with Hebrew / Greek script or Strong's numbers looks those words up as source
 * words (lemmas and matching forms); the remaining words are German and keep the per-word originals lookup.
 */
function OriginalsPanel({
  query,
  books,
  appLanguage,
  onNavigate,
}: {
  query: string;
  books: BibleStructureBook[];
  appLanguage: string;
  onNavigate: () => void;
}) {
  const { t } = useTranslation();
  const { source, german } = splitSourceWords(originalsQueryOf(query));
  const hasGerman = german !== "" && isLookupQuery(german);
  if (source === "" && !hasGerman) {
    return <EmptyState>{t("search.originals.prompt")}</EmptyState>;
  }
  return (
    <div className="grid gap-4">
      {source !== "" ? (
        <section aria-label={t("search.sourceWords.heading")} className="grid gap-3">
          {hasGerman ? (
            <h4 className="text-sm font-semibold text-fg-muted">
              {t("search.sourceWords.heading")}
            </h4>
          ) : null}
          <SourceWordsSection
            words={source}
            books={books}
            appLanguage={appLanguage}
            onNavigate={onNavigate}
          />
        </section>
      ) : null}
      {hasGerman ? <GermanOriginals words={german} onNavigate={onNavigate} /> : null}
    </div>
  );
}

/** "ἀγάπη G26 · Nominativ": how a slot's word was read plus its filters in words. */
function GrammarSlotChip({ slot, onNavigate }: { slot: GrammarSlot; onNavigate: () => void }) {
  const { t } = useTranslation();
  const strongsLink = slotStrongsLink(slot);
  const word =
    slot.readAs === "lemma" && slot.lemmas.length > 0
      ? slot.lemmas.map((lemma) => (
          <span key={lemma.strongs}>
            <OriginalText language={languageOfStrongs(lemma.strongs)}>{lemma.lemma}</OriginalText>{" "}
            <span className="text-fg-muted">{lemma.strongs}</span>
          </span>
        ))
      : slot.readAs === "german"
        ? t("search.grammar.germanWord", { word: germanReading(slot) })
        : t("search.grammar.anyWord");
  return (
    <li className="inline-flex flex-wrap items-center gap-1 rounded-md border border-border bg-surface-muted px-2 py-1 text-sm">
      {strongsLink ? (
        <Link to={strongsLink} className={linkClasses} onClick={onNavigate}>
          {word}
        </Link>
      ) : (
        word
      )}
      {slot.filters.map((filter) => (
        <span key={`${filter.german}-${filter.key}-${filter.negated}`}>
          {" · "}
          {formatGrammarFilter(filter, t)}
        </span>
      ))}
    </li>
  );
}

/** The typed tokens with the offending one marked, the message and the candidates that replace the token. */
function GrammarProblemBlock({
  query,
  problem,
  onReplace,
}: {
  query: string;
  problem: GrammarProblem;
  onReplace: (candidate: string) => void;
}) {
  const { t } = useTranslation();
  const known = [
    "unknownKey",
    "unknownValue",
    "ambiguousValue",
    "tooManySlots",
    "tooManyFilters",
    "emptyQuery",
    "unknownWord",
    "keyNotInLanguage",
  ];
  const message = t(
    `search.grammar.problems.${known.includes(problem.code) ? problem.code : "generic"}`,
    { token: problem.token ?? "" },
  );
  const tokens = query.split(/\s+/).filter(Boolean);
  return (
    <div role="alert" className="grid justify-items-start gap-2 text-sm">
      <ul aria-label={t("search.grammar.slots")} className="flex flex-wrap gap-2">
        {tokens.map((token, i) => (
          <li
            // Tokens of the typed query in order; the index is the identity.
            // biome-ignore lint/suspicious/noArrayIndexKey: see above
            key={i}
            data-problem={token === problem.token || undefined}
            className={`rounded-md border px-2 py-1 ${token === problem.token ? "border-sev-error text-sev-error" : "border-border text-fg-muted"}`}
          >
            {token}
          </li>
        ))}
      </ul>
      <p>{message}</p>
      {problem.candidates && problem.candidates.length > 0 && problem.token ? (
        <div className="flex flex-wrap items-center gap-2">
          <span className="text-fg-muted">{t("search.grammar.candidates")}</span>
          {problem.candidates.map((candidate) => (
            <Button
              key={candidate}
              variant="secondary"
              size="sm"
              aria-label={t("search.grammar.replace", { token: problem.token, candidate })}
              onClick={() => onReplace(candidate)}
            >
              {candidate}
            </Button>
          ))}
        </div>
      ) : null}
    </div>
  );
}

/**
 * Per match: original form, lemma and the decoded grammar in words; "nicht übersetzt" without German words.
 * A match of a German-only slot shows the German word and the slot's `dt.` filters instead.
 */
function GrammarMatchLines({ hit, slots }: { hit: GrammarHit; slots: GrammarSlot[] }) {
  const { t } = useTranslation();
  return (
    <span className="grid gap-0.5 text-sm text-fg-muted">
      {hit.matches.map((match, i) => {
        if (!match.original) {
          const filters = (slots[match.slot]?.filters ?? [])
            .filter((filter) => filter.german)
            .map((filter) => formatGrammarFilter(filter, t));
          return (
            // Matches keep the server order; the index is the identity.
            // biome-ignore lint/suspicious/noArrayIndexKey: see above
            <span key={i} className="block" data-testid="grammar-match">
              <span className="text-base text-fg">{germanMatchText(hit, match)}</span>
              {" · "}
              {[t("search.grammar.germanMatch"), ...filters].join(", ")}
            </span>
          );
        }
        const original = match.original;
        const language = languageOfOriginal(original);
        return (
          // Matches keep the server order; the index is the identity.
          // biome-ignore lint/suspicious/noArrayIndexKey: see above
          <span key={i} className="block" data-testid="grammar-match">
            <span className="text-base text-fg">
              <OriginalText language={language}>{original.form}</OriginalText>
            </span>
            {sameWord(original.form, original.lemma) ? null : (
              <>
                {" · "}
                <OriginalText language={language}>{original.lemma}</OriginalText>
              </>
            )}
            {" · "}
            {formatGrammarWords(original.keys, t, original.code)}
            {match.germanPositions.length === 0 ? ` (${t("search.grammar.notTranslated")})` : null}
          </span>
        );
      })}
    </span>
  );
}

/** Form and lemma spelled alike once the printed punctuation is dropped (the lemma is then not repeated). */
function sameWord(form: string, lemma: string): boolean {
  const letters = (s: string) => s.replace(/[^\p{L}\p{M}]/gu, "");
  return letters(form) === letters(lemma);
}

/** The grammar result in the originals column: slot chips or the problem, summary, book chart and hits. */
function GrammarPanel({
  query,
  search,
  books,
  language,
  bookId,
  onSelectBook,
  listId,
  activeIndex,
  onOpenHit,
  onReplace,
  onNavigate,
}: {
  query: string;
  search: ReturnType<typeof useGrammarSearch>;
  books: BibleStructureBook[];
  language: string;
  bookId: number | null;
  onSelectBook: (bookId: number | null) => void;
  listId: string;
  activeIndex: number;
  onOpenHit: (index: number) => void;
  onReplace: (candidate: string) => void;
  onNavigate: () => void;
}) {
  const { t } = useTranslation();
  const pages = search.data?.pages ?? [];
  const first = pages[0];
  const grammarHits = pages.flatMap((page) => page.hits);
  const hits = grammarHits.map(toVerseHit);
  const dimmed = search.isPlaceholderData || (search.isFetching && !search.isFetchingNextPage);
  const selectedBook = bookId === null ? undefined : books.find((b) => b.id === bookId);

  if (search.isError) {
    const problem = parseGrammarProblem(search.error);
    if (problem)
      return <GrammarProblemBlock query={query} problem={problem} onReplace={onReplace} />;
    return (
      <div role="alert" className="grid justify-items-start gap-2 text-sm">
        <p>{describeApiError(search.error, t("search.grammar.error"))}</p>
        <Button variant="secondary" size="sm" onClick={() => search.refetch()}>
          {t("search.retry")}
        </Button>
      </div>
    );
  }
  if (!first) return <EmptyState>{t("search.loading")}</EmptyState>;
  return (
    <div className={`grid gap-3 ${dimmed ? "opacity-60" : ""}`}>
      <ul aria-label={t("search.grammar.slots")} className="flex flex-wrap gap-2">
        {first.slots.map((slot, i) => (
          // Slots are in query order; the index is the identity.
          // biome-ignore lint/suspicious/noArrayIndexKey: see above
          <GrammarSlotChip key={i} slot={slot} onNavigate={onNavigate} />
        ))}
      </ul>
      <p role="status" aria-live="polite" className="text-sm text-fg-muted">
        {dimmed
          ? ""
          : `${t("search.grammar.occurrences", { count: first.totalWords })} ${t("search.grammar.inVerses", { count: first.totalVerses })}`}
      </p>
      {first.totalVerses > 0 && books.length > 0 ? (
        <details className="rounded-md border border-border p-3">
          <summary className="cursor-pointer text-sm font-medium">
            {t("search.books.filterBy")}
          </summary>
          <div className="mt-3">
            <BookBarChart
              books={books}
              counts={first.books}
              testaments={["ot", "nt"]}
              title={t("search.books.title")}
              summary={t("search.books.summary", {
                count: first.totalVerses,
                books: first.books.length,
                total: books.length,
              })}
              selectedBookId={bookId}
              onSelectBook={onSelectBook}
            />
          </div>
        </details>
      ) : null}
      {selectedBook ? (
        <div>
          <Button
            variant="secondary"
            size="sm"
            aria-label={t("search.filter.remove", { book: bookName(selectedBook, language) })}
            onClick={() => onSelectBook(null)}
          >
            {t("search.filter.book", { book: bookName(selectedBook, language) })}
            <span aria-hidden="true">✕</span>
          </Button>
        </div>
      ) : null}
      {first.totalVerses === 0 ? (
        <EmptyState>{t("search.grammar.empty", { query })}</EmptyState>
      ) : (
        <VerseHitList
          id={listId}
          label={t("search.results")}
          hits={hits}
          books={books}
          language={language}
          activeIndex={activeIndex}
          onSelect={(hit) => onOpenHit(hits.indexOf(hit))}
          renderDetails={(_, index) => {
            const hit = grammarHits[index];
            return hit ? <GrammarMatchLines hit={hit} slots={first.slots} /> : null;
          }}
          onLoadMore={() => search.fetchNextPage()}
          hasMore={search.hasNextPage}
          loading={search.isFetchingNextPage}
        />
      )}
    </div>
  );
}

/** Example queries of the cheat sheet; clicking one runs it. */
const GRAMMAR_EXAMPLES = ["agape Kasus:nom", "Tempus:aor Diathese:pass", "bara Stamm:qal"] as const;
const GERMAN_EXAMPLE = "Tempus:aor Modus:part dt.Verbform:finit";

/** Values of a key by their German label, with the English label in an English UI when it differs. */
function cheatSheetValues(values: GrammarVocabularyValue[], english: boolean): string {
  return values
    .map((value) =>
      english && foldGrammar(value.labelEn) !== foldGrammar(value.labelDe)
        ? `${value.labelDe} (${value.labelEn})`
        : value.labelDe,
    )
    .join(" · ");
}

/** One key of the cheat sheet: name, aliases, language note; values or what a free value looks like. */
function CheatSheetKey({
  name,
  vocabularyKey,
  values,
}: {
  name: string;
  vocabularyKey: GrammarVocabularyKey;
  values: GrammarVocabularyValue[];
}) {
  const { t, i18n } = useTranslation();
  const note = languageNote(vocabularyKey.languages, t);
  return (
    <div className="grid gap-0.5">
      <dt>
        <code className="font-medium text-fg">{name}:</code>
        {vocabularyKey.aliases.length > 0 ? (
          <span className="text-fg-muted"> ({vocabularyKey.aliases.join(", ")})</span>
        ) : null}
        {note ? <span className="text-fg-muted"> · {note}</span> : null}
      </dt>
      <dd className="text-fg-muted">
        {values.length > 0
          ? cheatSheetValues(values, i18n.language.startsWith("en"))
          : t(`search.help.free.${vocabularyKey.name}`, {
              defaultValue: t("search.help.free.generic"),
            })}
      </dd>
    </div>
  );
}

/**
 * Grammar cheat sheet (roadmap 0029.9), generated from the vocabulary: syntax, example queries, every key with its
 * aliases and values, the `dt.` keys with their German values and the known limits.
 */
function GrammarCheatSheet({
  id,
  vocabulary,
  onExample,
}: {
  id: string;
  vocabulary: GrammarVocabulary | undefined;
  onExample: (query: string) => void;
}) {
  const { t } = useTranslation();
  const example = (query: string) => (
    <Button
      key={query}
      variant="secondary"
      size="sm"
      aria-label={t("search.help.run", { query })}
      onClick={() => onExample(query)}
    >
      <code>{query}</code>
    </Button>
  );
  if (!vocabulary) return <EmptyState>{t("search.loading")}</EmptyState>;
  const originalKeys = vocabulary.keys.filter((key) => !key.germanOnly);
  const germanKeys = vocabulary.germanKeysEnabled
    ? vocabulary.keys.filter((key) => key.germanPrefixAllowed)
    : [];
  return (
    <section
      id={id}
      aria-labelledby={`${id}-title`}
      className="grid max-h-80 gap-3 overflow-y-auto rounded-md border border-border bg-surface-muted p-3 text-sm"
    >
      <h4 id={`${id}-title`} className="font-semibold">
        {t("search.help.title")}
      </h4>
      <p>{t("search.help.intro")}</p>
      <div className="flex flex-wrap items-center gap-2">
        <span className="text-fg-muted">{t("search.help.examples")}</span>
        {GRAMMAR_EXAMPLES.map(example)}
      </div>
      <dl className="grid gap-2 sm:grid-cols-2">
        {originalKeys.map((key) => (
          <CheatSheetKey key={key.name} name={key.name} vocabularyKey={key} values={key.values} />
        ))}
      </dl>
      {germanKeys.length > 0 ? (
        <>
          <h5 className="font-semibold">{t("search.help.germanTitle")}</h5>
          <p>{t("search.help.germanIntro", { prefix: vocabulary.germanPrefix })}</p>
          <div className="flex flex-wrap items-center gap-2">{example(GERMAN_EXAMPLE)}</div>
          <dl className="grid gap-2 sm:grid-cols-2">
            {germanKeys.map((key) => (
              <CheatSheetKey
                key={key.name}
                name={`${vocabulary.germanPrefix}${key.name}`}
                vocabularyKey={{ ...key, languages: [] }}
                values={key.germanValues ?? []}
              />
            ))}
          </dl>
          <p className="text-fg-muted">{t("search.help.germanNote")}</p>
        </>
      ) : null}
      <p className="text-fg-muted">{t("search.help.statusNote")}</p>
    </section>
  );
}

const SUGGEST_DEBOUNCE_MS = 250;
const MAX_LIST = 10;

/** "nur Griechisch" / "nur Hebräisch" for a key or value limited to one language family. */
function languageNote(languages: string[], t: (key: string) => string): string | undefined {
  const limit = languageLimit(languages);
  return limit ? t(`search.suggest.only.${limit}`) : undefined;
}

/**
 * Autocomplete of the search field (roadmap 0029.3f/0029.4/0029.5f): vocabulary keys and values for the token under the
 * caret (instant), lemma suggestions for a word token (debounced lookup, accepting inserts the Strong's number) and
 * counts for the values of the slot (debounced facets; values without occurrences are dimmed).
 */
function useSearchSuggestions(
  open: boolean,
  input: string,
  caret: number,
  vocabulary: GrammarVocabulary | undefined,
) {
  const { t, i18n } = useTranslation();
  const token = tokenAtCaret(input, caret, vocabulary);
  const vocabularyHits =
    open && token && vocabulary ? vocabularySuggestions(input, token, vocabulary) : [];

  const lemmaWord = open ? lemmaLookupWord(token) : "";
  const debouncedWord = useDebouncedValue(lemmaWord, SUGGEST_DEBOUNCE_MS);
  const lookup = useSourceWordsLookup(debouncedWord === lemmaWord ? lemmaWord : "", 8);
  const lemmaHits =
    token && lemmaWord !== "" && debouncedWord === lemmaWord
      ? lemmaSuggestions(token, lookup.data?.tokens[0]?.lemmas ?? [])
      : [];

  const request = open && vocabulary ? facetRequest(input, token, vocabulary) : null;
  const requestKey = request ? JSON.stringify(request) : "";
  const debouncedKey = useDebouncedValue(requestKey, SUGGEST_DEBOUNCE_MS);
  const facets = useGrammarFacets(
    request && debouncedKey === requestKey
      ? (JSON.parse(debouncedKey) as GrammarFacetRequest)
      : null,
  );
  const facet =
    request && debouncedKey === requestKey && facets.isSuccess
      ? facets.data.facets.find((f) => f.key === request.key)
      : undefined;
  const counts =
    facets.isSuccess && facet ? new Map(facet.values.map((v) => [v.key, v.count])) : null;

  const suggestions: GrammarSuggestion[] = [...vocabularyHits, ...lemmaHits].slice(0, MAX_LIST);
  const english = i18n.language.startsWith("en");
  const items: SuggestionItem[] = suggestions.map((s, index) => {
    const id = `${s.kind}-${s.insert}-${index}`;
    if (s.kind === "lemma" && s.lemma) {
      return {
        id,
        label: (
          <>
            <OriginalText language={s.lemma.language}>{s.lemma.lemma}</OriginalText>{" "}
            <span className="font-medium">{s.lemma.strongs}</span>
          </>
        ),
        detail: s.lemma.gloss || undefined,
        trailing: t("search.sourceWords.times", { count: s.lemma.occurrences }),
      };
    }
    const value = s.value;
    const languages = value?.languages ?? s.key?.languages ?? [];
    if (s.kind === "value" && value) {
      const count = counts?.get(value.key);
      return {
        id,
        label: english ? value.labelEn : value.labelDe,
        detail: languageNote(languages, t),
        trailing: count ? t("search.suggest.count", { count }) : undefined,
        dimmed: counts !== null && !count,
      };
    }
    return {
      id,
      label: s.insert,
      detail:
        [value ? (english ? value.labelEn : value.labelDe) : undefined, languageNote(languages, t)]
          .filter(Boolean)
          .join(" · ") || undefined,
    };
  });
  return { token, suggestions, items };
}

/**
 * Search palette over the reader: verse full text search (ranked hits with the matched words marked,
 * hits per book as filter) next to the original words of every query word. Opened from the menu
 * (magnifier or `F`); the typed text and the filter survive closing.
 */
export function SearchOverlay({ open, onClose }: SearchOverlayProps) {
  const { t, i18n } = useTranslation();
  const navigate = useNavigate();
  const coarse = useCoarsePointer();
  const listId = useId();
  const inputRef = useRef<HTMLInputElement>(null);
  // Query, tab and marking live in the shared search state (localStorage): closing, following a hit and a reload
  // keep them; only the clear button or deleting the text removes the query.
  const { query: input, tab, mark } = useSearchState();
  const setInput = setSearchQuery;
  const setTab = setSearchTab;
  const { query, submit } = useSubmittedQuery(input);
  // Autocomplete: caret position, whether the list may show (typing opens it, Esc / accepting a value closes it),
  // the highlighted entry for the current text and caret.
  const vocabulary = useGrammarVocabulary(open).data;
  const [helpOpen, setHelpOpen] = useState(false);
  const helpId = useId();
  const [caret, setCaret] = useState(() => input.length);
  const [listWanted, setListWanted] = useState(false);
  const [focused, setFocused] = useState(false);
  const pendingCaret = useRef<number | null>(null);
  const suggest = useSearchSuggestions(open && listWanted && focused, input, caret, vocabulary);
  const suggestionKey = `${input}|${caret}`;
  const [pickedSuggestion, setPickedSuggestion] = useState({ key: "", index: -1 });
  // Inside a filter or a grammar query the first entry is preselected (Enter accepts it); for a plain word Tab
  // accepts the first entry and Enter still searches.
  const preselect = suggest.token?.text.includes(":") || isGrammarQuery(input) ? 0 : -1;
  const activeSuggestion =
    pickedSuggestion.key === suggestionKey ? pickedSuggestion.index : preselect;

  useEffect(() => {
    if (!open) setListWanted(false);
  }, [open]);

  // After accepting a suggestion the caret goes behind the inserted text (runs once the new text is rendered).
  useLayoutEffect(() => {
    const position = pendingCaret.current;
    if (position === null || !inputRef.current) return;
    pendingCaret.current = null;
    inputRef.current.setSelectionRange(position, position);
  });

  const acceptSuggestion = (index: number) => {
    const suggestion = suggest.suggestions[index];
    if (!suggestion) return;
    const next = applySuggestion(input, suggestion);
    pendingCaret.current = next.caret;
    setCaret(next.caret);
    setInput(next.value);
    // After a key the values follow at once; a value or a lemma closes the list.
    setListWanted(suggestion.kind === "key");
  };
  // The book filter belongs to the query it was set for.
  const [filter, setFilter] = useState<{ query: string; bookId: number | null }>({
    query: "",
    bookId: null,
  });
  const bookId = filter.query === query ? filter.bookId : null;
  const [cursor, setCursor] = useState({ key: "", index: -1 });
  const resultKey = `${query}|${bookId ?? 0}`;
  const activeIndex = cursor.key === resultKey ? cursor.index : -1;

  const structure = useQuery({ ...structureQuery(), enabled: open });
  const books = structure.data ?? [];
  const reader = useReaderLevel();
  const grammar = isGrammarQuery(query);
  const parse = useReferenceParse(open && !grammar ? query : "");
  const reference = viewOfParse(parse.data);
  // Full-text search is skipped for a reference. A query with a digit may be one, so it waits for the parse
  // answer (a failed parse counts as "no reference"); plain words search at once.
  const waitsForParse = parse.isPending && /\d/.test(query);
  const searchQuery = open && !grammar && !waitsForParse && !reference.skipsFullText ? query : "";
  const grammarSearch = useGrammarSearch(open && grammar ? query : "", bookId);
  const search = useVerseSearch(
    searchQuery,
    bookId,
    VERSE_SEARCH_PAGE_SIZE,
    reader.level,
    reader.ready,
  );
  const revisedLabel = (level: number) =>
    t("bible.revision.revisedMark", {
      level: reader.levels.find((l) => l.level === level)?.name ?? level,
    });
  const language = i18n.language;

  const pages = search.data?.pages ?? [];
  const first = pages[0];
  const verseHits = pages.flatMap((page) => page.hits);
  // The keyboard drives the visible list: the grammar hits for a grammar query, else the verse hits.
  const hits = grammar
    ? (grammarSearch.data?.pages ?? []).flatMap((page) => page.hits).map(toVerseHit)
    : verseHits;
  const searching = searchQuery !== "";
  const invalid = searching && !isVerseSearchQuery(searchQuery);
  const showReference =
    open &&
    query !== "" &&
    (reference.passages.length > 0 ||
      reference.books.length > 0 ||
      reference.error !== null ||
      (parse.data?.bookCandidates.length ?? 0) > 0);
  const dimmed = search.isPlaceholderData || (search.isFetching && !search.isFetchingNextPage);

  useTrackSearch("verses", searchQuery, first && !dimmed ? first.total : undefined);
  useTrackSearch(
    "references",
    showReference && !parse.isPending ? query : "",
    reference.passages.length,
  );

  const openHit = (index: number) => {
    const hit = hits[index];
    if (!hit) return;
    navigate(hitPath(hit));
    onClose();
  };

  // Enter on a reference jumps to it; when the answer is not there yet, the jump waits for it.
  const pendingEnter = useRef("");
  const referencePath = firstReferencePath(parse.data);
  useEffect(() => {
    if (pendingEnter.current === "" || pendingEnter.current !== query || parse.isPending) return;
    pendingEnter.current = "";
    if (referencePath) {
      navigate(referencePath);
      onClose();
    }
  }, [query, parse.isPending, referencePath, navigate, onClose]);

  const onKeyDown = (event: KeyboardEvent<HTMLInputElement>) => {
    if (event.key === "ArrowDown" && hits.length > 0) {
      event.preventDefault();
      setCursor({ key: resultKey, index: Math.min(activeIndex + 1, hits.length - 1) });
    } else if (event.key === "ArrowUp" && hits.length > 0) {
      event.preventDefault();
      setCursor({ key: resultKey, index: Math.max(activeIndex - 1, -1) });
    } else if (event.key === "Enter") {
      event.preventDefault();
      if (activeIndex >= 0) openHit(activeIndex);
      else if (referencePath && query !== "" && normalized(input) === query) {
        navigate(referencePath);
        onClose();
      } else {
        pendingEnter.current = normalized(input);
        submit();
      }
    }
  };

  const selectedBook = bookId === null ? undefined : books.find((b) => b.id === bookId);
  const showList = searching && !invalid && first !== undefined;

  const versesColumn = (
    <section
      aria-label={t("search.tabs.verses")}
      aria-busy={dimmed || undefined}
      className={`grid min-h-0 content-start gap-3 overflow-y-auto p-4 ${tab === "verses" ? "" : "max-lg:hidden"}`}
    >
      <h3 className="text-sm font-semibold text-fg-muted max-lg:hidden">
        {t("search.tabs.verses")}
      </h3>
      {/* Polite live region: announced after each result */}
      <p role="status" aria-live="polite" className="text-sm text-fg-muted">
        {showList && !dimmed ? t("search.count", { count: first.total }) : ""}
      </p>
      {showReference ? (
        <ReferenceCard
          passages={reference.passages}
          bookRefs={reference.books}
          error={reference.error}
          bookCandidates={parse.data?.bookCandidates}
          books={books}
          language={language}
          level={reader.level}
          onNavigate={onClose}
          onPickBook={(book) => setInput(replaceTypedBook(input, bookName(book, language)))}
        />
      ) : null}
      {query === "" ? <EmptyState>{t("search.hint")}</EmptyState> : null}
      {open && grammar ? (
        <div className="flex flex-wrap items-center gap-2 text-sm text-fg-muted">
          <p>{t("search.grammar.hint")}</p>
          <Button
            variant="secondary"
            size="sm"
            className="lg:hidden"
            onClick={() => setTab("originals")}
          >
            {t("search.grammar.showOriginals")}
          </Button>
        </div>
      ) : null}
      {invalid ? (
        <p role="alert" className="text-sm text-fg">
          {t("search.tooLong")}
        </p>
      ) : null}
      {searching && !invalid && search.isError ? (
        <div role="alert" className="grid justify-items-start gap-2 text-sm">
          <p>{describeApiError(search.error, t("search.error"))}</p>
          {!(search.error instanceof ApiError && search.error.status === 400) ? (
            <Button variant="secondary" size="sm" onClick={() => search.refetch()}>
              {t("search.retry")}
            </Button>
          ) : null}
        </div>
      ) : null}
      {searching && !invalid && !search.isError && !first ? (
        <EmptyState>{t("search.loading")}</EmptyState>
      ) : null}
      {showList ? (
        <div className={`grid gap-3 ${dimmed ? "opacity-60" : ""}`}>
          {first.tokens
            .filter((token) => token.lemmas && token.lemmas.length > 0)
            .map((token) => (
              <p key={token.text} className="text-sm text-fg-muted" data-testid="widened">
                {t("search.widened", { word: token.text, lemmas: token.lemmas?.join(", ") })}
              </p>
            ))}
          {first.total > 0 && books.length > 0 ? (
            <details className="rounded-md border border-border p-3">
              <summary className="cursor-pointer text-sm font-medium">
                {t("search.books.filterBy")}
              </summary>
              <div className="mt-3">
                <BookBarChart
                  books={books}
                  counts={first.bookCounts.map((c) => ({ bibleBookId: c.bookId, count: c.count }))}
                  testaments={["ot", "nt"]}
                  title={t("search.books.title")}
                  summary={t("search.books.summary", {
                    // The counts ignore the book filter, so their sum is the total of the whole query.
                    count: first.bookCounts.reduce((sum, c) => sum + c.count, 0),
                    books: first.bookCounts.length,
                    total: books.length,
                  })}
                  selectedBookId={bookId}
                  onSelectBook={(id) => setFilter({ query, bookId: id })}
                />
              </div>
            </details>
          ) : null}
          {selectedBook ? (
            <div>
              <Button
                variant="secondary"
                size="sm"
                aria-label={t("search.filter.remove", { book: bookName(selectedBook, language) })}
                onClick={() => setFilter({ query, bookId: null })}
              >
                {t("search.filter.book", { book: bookName(selectedBook, language) })}
                <span aria-hidden="true">✕</span>
              </Button>
            </div>
          ) : null}
          {first.total === 0 ? (
            <EmptyState>{t("search.empty", { query: searchQuery })}</EmptyState>
          ) : (
            <>
              <VerseHitList
                id={listId}
                label={t("search.results")}
                hits={verseHits}
                books={books}
                language={language}
                activeIndex={activeIndex}
                revisedLabel={revisedLabel}
                onSelect={(hit) => openHit(verseHits.indexOf(hit))}
                onLoadMore={() => search.fetchNextPage()}
                hasMore={search.hasNextPage}
                loading={search.isFetchingNextPage}
              />
              {search.hasNextPage ? (
                <p className="text-xs text-fg-muted">
                  {t("search.shown", { shown: verseHits.length, total: first.total })}
                </p>
              ) : null}
            </>
          )}
        </div>
      ) : null}
    </section>
  );

  const originalsColumn = (
    <section
      aria-label={t("search.tabs.originals")}
      className={`grid min-h-0 content-start gap-3 overflow-y-auto p-4 ${tab === "originals" ? "" : "max-lg:hidden"}`}
    >
      <h3 className="text-sm font-semibold text-fg-muted max-lg:hidden">
        {t("search.originals.heading")}
      </h3>
      {grammar ? (
        <GrammarPanel
          query={query}
          search={grammarSearch}
          books={books}
          language={language}
          bookId={bookId}
          onSelectBook={(id) => setFilter({ query, bookId: id })}
          listId={listId}
          activeIndex={activeIndex}
          onOpenHit={openHit}
          onNavigate={onClose}
          onReplace={(candidate) => {
            const problem = parseGrammarProblem(grammarSearch.error);
            if (problem) setInput(replaceGrammarToken(input, problem, candidate));
          }}
        />
      ) : (
        <OriginalsPanel query={query} books={books} appLanguage={language} onNavigate={onClose} />
      )}
    </section>
  );

  return (
    <Dialog
      open={open}
      onClose={onClose}
      title={t("search.title")}
      titleHidden
      size="lg"
      placement="top"
      bare
      // On touch devices the keyboard would cover a restored result, so only an empty search focuses the field.
      initialFocusRef={coarse && input !== "" ? undefined : inputRef}
    >
      <div className="grid gap-2 border-b border-border px-4 pt-2 pb-3">
        <div className="relative">
          <TextField
            type="search"
            label={t("search.label")}
            labelHidden
            required={false}
            value={input}
            onChange={(value) => {
              setInput(value);
              setCaret(inputRef.current?.selectionStart ?? value.length);
              setListWanted(true);
            }}
            onSelect={(event) => setCaret(event.currentTarget.selectionStart ?? input.length)}
            onFocus={() => setFocused(true)}
            onBlur={() => setFocused(false)}
            suggestions={suggest.items}
            suggestionsLabel={t("search.suggest.label")}
            activeSuggestion={activeSuggestion}
            onActiveSuggestionChange={(index) => setPickedSuggestion({ key: suggestionKey, index })}
            onSuggestionAccept={acceptSuggestion}
            onSuggestionsDismiss={() => setListWanted(false)}
            inputRef={inputRef}
            placeholder={t("search.placeholder")}
            autoComplete="off"
            spellCheck={false}
            enterKeyHint="search"
            role="combobox"
            aria-autocomplete="list"
            aria-expanded={hits.length > 0}
            aria-controls={hits.length > 0 ? listId : undefined}
            aria-activedescendant={activeIndex >= 0 ? hitOptionId(listId, activeIndex) : undefined}
            onKeyDown={onKeyDown}
            className="search-field pe-10"
          />
          {input !== "" && (
            <Button
              variant="ghost"
              size="sm"
              className="absolute inset-y-1 end-1"
              aria-label={t("search.clear")}
              title={t("search.clear")}
              onClick={() => {
                setInput("");
                inputRef.current?.focus();
              }}
            >
              <svg
                aria-hidden="true"
                viewBox="0 0 24 24"
                className="size-4"
                fill="none"
                stroke="currentColor"
                strokeWidth="2"
                strokeLinecap="round"
              >
                <path d="M18 6 6 18M6 6l12 12" />
              </svg>
            </Button>
          )}
        </div>
        <div className="flex flex-wrap items-center justify-between gap-2 text-sm">
          <div className="flex flex-wrap items-center gap-2">
            <Switch checked={mark} onCheckedChange={setSearchMark}>
              {t("search.mark.switch")}
            </Switch>
            <Button
              variant="ghost"
              size="sm"
              aria-expanded={helpOpen}
              aria-controls={helpOpen ? helpId : undefined}
              aria-label={t("search.help.toggle")}
              title={t("search.help.toggle")}
              onClick={() => setHelpOpen((value) => !value)}
            >
              ?
            </Button>
          </div>
          <fieldset className="flex gap-2 lg:hidden">
            <legend className="sr-only">{t("search.tabs.label")}</legend>
            {(["verses", "originals"] as const).map((id) => (
              <Button
                key={id}
                variant="secondary"
                size="sm"
                aria-pressed={tab === id}
                onClick={() => setTab(id)}
              >
                {t(`search.tabs.${id}`)}
              </Button>
            ))}
          </fieldset>
        </div>
        {helpOpen ? (
          <GrammarCheatSheet
            id={helpId}
            vocabulary={vocabulary}
            onExample={(example) => {
              setInput(example);
              setTab("originals");
              setHelpOpen(false);
              inputRef.current?.focus();
            }}
          />
        ) : null}
      </div>
      <div className="grid min-h-0 flex-1 grid-rows-1 lg:grid-cols-2 lg:divide-x lg:divide-border">
        {versesColumn}
        {originalsColumn}
      </div>
    </Dialog>
  );
}
