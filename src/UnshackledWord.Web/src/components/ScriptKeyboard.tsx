import { SimpleKeyboard } from "simple-keyboard";
import "simple-keyboard/build/css/index.css";
import { useEffect, useRef } from "react";
import { useTranslation } from "react-i18next";
import { BACKSPACE_KEY, type ScriptKeyboardLayout, scriptRows } from "@/lib/vocab/scriptLayouts";

export interface ScriptKeyboardProps {
  layout: ScriptKeyboardLayout;
  /** The letter of the pressed key; `"⌫"` is backspace. */
  onKey: (key: string) => void;
}

// simple-keyboard's own control token for the backspace key, shown as ⌫.
const BACKSPACE_TOKEN = "{bksp}";

/**
 * On-screen keyboard for Greek and Hebrew (roadmap 0022.10f, port of Lexio 0049 on `simple-keyboard`).
 * The keys sit in physical QWERTY position, so the panel is also the legend of the physical key mapping
 * (`remapPhysicalKey`). The panel is always left to right, also for Hebrew. It never takes focus, so a tap
 * does not move the cursor of the letter boxes. Colours are the design tokens (`.script-keyboard` in `index.css`).
 */
export function ScriptKeyboard({ layout, onKey }: ScriptKeyboardProps) {
  const { t } = useTranslation();
  const mount = useRef<HTMLDivElement>(null);
  // The callback goes through a ref so the keyboard is only rebuilt when the layout changes.
  const onKeyRef = useRef(onKey);
  useEffect(() => {
    onKeyRef.current = onKey;
  });
  const backspaceLabel = t("learn.typing.backspace");

  useEffect(() => {
    const element = mount.current;
    if (!element) return;
    const keyboard = new SimpleKeyboard(element, {
      layout: {
        default: scriptRows(layout).map((row) =>
          row.map((key) => (key === BACKSPACE_KEY ? BACKSPACE_TOKEN : key)).join(" "),
        ),
      },
      display: { [BACKSPACE_TOKEN]: BACKSPACE_KEY },
      theme: "hg-theme-default",
      preventMouseDownDefault: true,
      stopMouseDownPropagation: true,
      useButtonTag: true,
      buttonAttributes: [
        { attribute: "aria-label", value: backspaceLabel, buttons: BACKSPACE_TOKEN },
      ],
      onKeyPress: (button: string) =>
        onKeyRef.current(button === BACKSPACE_TOKEN ? BACKSPACE_KEY : button),
    });
    return () => keyboard.destroy();
  }, [layout, backspaceLabel]);

  return (
    <fieldset
      aria-label={t("learn.typing.keyboard")}
      lang={layout === "hebrew" ? "he" : "grc"}
      dir="ltr"
      className="script-keyboard"
      onMouseDown={(event) => event.preventDefault()}
    >
      <div ref={mount} className="script-keyboard-mount" />
    </fieldset>
  );
}
