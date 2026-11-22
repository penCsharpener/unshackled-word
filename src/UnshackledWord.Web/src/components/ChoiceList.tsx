import { Button } from "./Button";

export interface ChoiceListProps {
  /** Option texts in display order; the index is the option's position. */
  options: string[];
  /** Script of the options: `de` (default) or the original scripts `grc` / `he`. */
  lang?: "de" | "grc" | "he";
  /** Index of an option that is plain UI text (the "none of the above" option), never set in the original script. */
  plainIndex?: number | null;
  /** `picked` is the chosen index; `correct` is the right one, set once the answer is back. */
  state: { picked: number | null; correct: number | null };
  onPick: (index: number) => void;
}

/**
 * Multiple-choice options as a list of buttons (roadmap 0022.8f). After a pick the list is locked:
 * the correct option gets `border-sev-ok`, a wrong pick `border-sev-error`. The keys 1–5 are
 * handled by the study page, which calls `onPick`.
 */
export function ChoiceList({
  options,
  lang = "de",
  plainIndex = null,
  state,
  onPick,
}: ChoiceListProps) {
  const locked = state.picked !== null;
  return (
    <ol className="grid gap-2 md:grid-cols-2">
      {options.map((text, index) => {
        const plain = plainIndex === index;
        const original = lang !== "de" && !plain;
        const isCorrect = state.correct === index;
        const isWrongPick = state.picked === index && state.correct !== null && !isCorrect;
        return (
          <li key={text}>
            <Button
              variant="secondary"
              size="md"
              lang={plain ? undefined : lang}
              dir={lang === "he" && !plain ? "rtl" : undefined}
              aria-disabled={locked || undefined}
              className={`w-full justify-start ${original ? "font-original text-xl" : ""} ${
                isCorrect ? "border-sev-ok" : ""
              } ${isWrongPick ? "border-sev-error" : ""}`}
              onClick={() => {
                if (!locked) onPick(index);
              }}
            >
              <span aria-hidden="true" className="w-5 text-sm text-fg-muted">
                {index + 1}
              </span>
              {text}
            </Button>
          </li>
        );
      })}
    </ol>
  );
}
