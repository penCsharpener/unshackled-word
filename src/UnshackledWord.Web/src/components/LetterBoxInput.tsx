import { type ChangeEvent, type KeyboardEvent, useEffect, useRef } from "react";
import { useTranslation } from "react-i18next";
import { remapPhysicalKey } from "@/lib/vocab/scriptLayouts";
import {
  groupLengths,
  isLetter,
  removeLetter,
  setLetter,
  type TypingLayout,
} from "@/lib/vocab/typing";

export interface LetterBoxInputProps {
  /** Groups (number of boxes) and the fixed separators between them; the letters are never part of it. */
  layout: TypingLayout;
  /** One string per group, filled from the start (no gaps). */
  value: string[];
  onChange: (groups: string[]) => void;
  dir?: "ltr" | "rtl";
  lang: "grc" | "he";
  readOnly?: boolean;
  /** Per group, whether the typed text was right (set once the answer is back); drives the box borders. */
  results?: boolean[];
}

interface Slot {
  group: number;
  position: number;
}

/**
 * Typing card input (roadmap 0022.10f): one box per letter, grouped, with the separators printed between
 * the groups. The cursor jumps to the next group when a group is full and Backspace goes back. Latin keys
 * type Greek or Hebrew letters (same table as the `ScriptKeyboard`). The on-screen keyboard has no focus
 * of its own, so the page edits `value` with `pushLetter` / `popLetter` and the cursor follows.
 */
export function LetterBoxInput({
  layout,
  value,
  onChange,
  dir = "ltr",
  lang,
  readOnly = false,
  results,
}: LetterBoxInputProps) {
  const { t } = useTranslation();
  const root = useRef<HTMLFieldSetElement>(null);
  const boxes = useRef<(HTMLInputElement | null)[]>([]);
  // Slot to focus once the new value is rendered; unset for changes from outside (on-screen keyboard).
  const pendingFocus = useRef<number | null>(null);

  const lengths = groupLengths(layout);
  const slots: Slot[] = lengths.flatMap((length, group) =>
    Array.from({ length }, (_, position) => ({ group, position })),
  );
  const slotIndex = (group: number, position: number) =>
    slots.findIndex((slot) => slot.group === group && slot.position === position);
  // The first box without a letter, or the last box when everything is filled.
  const caretSlot = () => {
    const free = slots.findIndex(
      ({ group, position }) => [...(value[group] ?? "")].length <= position,
    );
    return free < 0 ? slots.length - 1 : free;
  };

  // Runs when the letters change (and on mount), so a click on a box is never overruled.
  const typed = value.join("\u0001");
  // biome-ignore lint/correctness/useExhaustiveDependencies: `typed` stands for `value`; the slots follow from `layout`
  useEffect(() => {
    if (readOnly) return;
    const inside = root.current?.contains(document.activeElement) ?? false;
    const idle = document.activeElement === document.body || document.activeElement === null;
    const target = pendingFocus.current ?? (inside || idle ? caretSlot() : null);
    pendingFocus.current = null;
    if (target !== null && target >= 0) boxes.current[target]?.focus();
  }, [typed, readOnly]);

  function put(group: number, position: number, ch: string) {
    const next = setLetter(layout, value, group, position, ch);
    if (next === value) return;
    onChange(next);
    // The cursor goes to the next box; at the end of a group it jumps to the next group.
    const at = slotIndex(group, position);
    pendingFocus.current = Math.min(at + 1, slots.length - 1);
  }

  function handleKeyDown(event: KeyboardEvent<HTMLInputElement>, slot: Slot, index: number) {
    if (event.altKey || event.ctrlKey || event.metaKey) return;
    const chars = [...(value[slot.group] ?? "")];
    if (event.key === "Backspace") {
      event.preventDefault();
      // A filled box is cleared and the cursor stays; an empty one steps back and clears the box before.
      if (slot.position < chars.length) {
        onChange(removeLetter(value, slot.group, slot.position));
        pendingFocus.current = index;
      } else if (index > 0 && slots[index - 1]) {
        const before = slots[index - 1] as Slot;
        onChange(removeLetter(value, before.group, before.position));
        pendingFocus.current = index - 1;
      }
      return;
    }
    if (event.key === "ArrowLeft" || event.key === "ArrowRight") {
      // Arrows follow the reading direction: in a right-to-left form the left arrow goes on.
      const forward = (event.key === "ArrowRight") === (dir === "ltr");
      const to = Math.min(Math.max(index + (forward ? 1 : -1), 0), slots.length - 1);
      event.preventDefault();
      boxes.current[to]?.focus();
      return;
    }
    if (event.key.length !== 1) return;
    event.preventDefault();
    const ch = remapPhysicalKey(lang === "he" ? "hebrew" : "greek", event.key) ?? event.key;
    if (isLetter(ch)) put(slot.group, slot.position, ch);
  }

  // Soft keyboards report no usable key in keydown; the box content is read instead.
  function handleInput(event: ChangeEvent<HTMLInputElement>, slot: Slot) {
    const ch = [...event.target.value].at(-1) ?? "";
    if (isLetter(ch)) put(slot.group, slot.position, ch);
  }

  let boxNumber = 0;
  return (
    <fieldset
      ref={root}
      dir={dir}
      lang={lang}
      aria-label={t("learn.typing.boxes")}
      className="flex flex-wrap items-end gap-1"
    >
      {layout.map((part, partIndex) => {
        if ("sep" in part) {
          return (
            <span
              // biome-ignore lint/suspicious/noArrayIndexKey: the layout is fixed for the card
              key={partIndex}
              aria-hidden="true"
              className="pb-2 font-original text-2xl text-fg-muted"
            >
              {part.sep.replace(/ /g, " ")}
            </span>
          );
        }
        const group = layout.slice(0, partIndex).filter((p) => "group" in p).length;
        const result = results?.[group];
        const tone =
          result === undefined ? "border-border" : result ? "border-sev-ok" : "border-sev-error";
        return (
          // biome-ignore lint/suspicious/noArrayIndexKey: the layout is fixed for the card
          <span key={partIndex} className="flex gap-1" data-group={group}>
            {Array.from({ length: part.group }, (_, position) => {
              const index = boxNumber;
              boxNumber += 1;
              const slot = { group, position };
              return (
                <input
                  // biome-ignore lint/suspicious/noArrayIndexKey: boxes never reorder
                  key={position}
                  ref={(element) => {
                    boxes.current[index] = element;
                  }}
                  value={[...(value[group] ?? "")][position] ?? ""}
                  maxLength={1}
                  inputMode="none"
                  disabled={readOnly}
                  autoComplete="off"
                  autoCapitalize="off"
                  spellCheck={false}
                  aria-label={t("learn.typing.letter")}
                  className={`h-10 w-8 rounded-md border bg-bg text-center font-original text-xl disabled:bg-surface-muted md:h-12 md:w-10 md:text-2xl ${tone}`}
                  onKeyDown={(event) => handleKeyDown(event, slot, index)}
                  onChange={(event) => handleInput(event, slot)}
                />
              );
            })}
          </span>
        );
      })}
    </fieldset>
  );
}
