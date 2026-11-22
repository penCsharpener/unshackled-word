import { Fragment } from "react";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import type { VocabLang, VocabSampleWord } from "@/lib/vocab/api";

export interface VerseContextProps {
  /** Reference of the verse, e.g. "Joh 1,1". */
  reference: string;
  /** Reader link, `/bible/{bookId}/{chapter}#v{verse}`; without it the reference is plain text (before an answer). */
  href?: string;
  /** Language of the original line: `grc` (Greek) or `he` (Hebrew, right to left). */
  lang: VocabLang;
  original: VocabSampleWord[];
  /** German line; its hit words are marked the same way, a hit word with `isGap` is a blank. */
  german: VocabSampleWord[];
}

/** Words in position order; hit words are marked, gap words are a blank line, the rest is plain text. */
function Words({ words }: { words: VocabSampleWord[] }) {
  const { t } = useTranslation();
  return [...words]
    .sort((a, b) => a.position - b.position)
    .map((word, index) => (
      <Fragment key={word.position}>
        {index > 0 ? " " : null}
        {word.isGap ? (
          <span
            role="img"
            aria-label={t("learn.verse.gap")}
            className="inline-block w-16 border-b-2 border-accent align-baseline"
          />
        ) : word.isHit ? (
          <mark className="rounded-sm bg-mark-linked px-0.5 text-fg">{word.text}</mark>
        ) : (
          word.text
        )}
      </Fragment>
    ));
}

/**
 * One sample verse with its hit words marked (roadmap 0022.8f): the reference links into the reader
 * when `href` is given, then the original and the German line. A verse-context question passes German
 * words with `isGap` and no `href`; the answer shows the full verse with the link. Shared by the study
 * cards and the vocab detail page.
 */
export function VerseContext({ reference, href, lang, original, german }: VerseContextProps) {
  return (
    <div className="grid gap-1 rounded-md bg-surface-muted p-3">
      {href ? (
        <Link to={href} className="text-sm font-medium text-accent underline">
          {reference}
        </Link>
      ) : (
        <span className="text-sm font-medium">{reference}</span>
      )}
      <p lang={lang} dir={lang === "he" ? "rtl" : undefined} className="font-original text-lg">
        <Words words={original} />
      </p>
      <p lang="de" className="font-scripture text-base">
        <Words words={german} />
      </p>
    </div>
  );
}
