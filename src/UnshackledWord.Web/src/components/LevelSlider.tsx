import { useEffect, useId, useRef, useState } from "react";

export interface LevelSliderProps {
  levels: { level: number; name: string }[];
  /** The selected level value (not the index). */
  value: number;
  onChange: (level: number) => void;
  /** Accessible name of the slider. */
  label: string;
}

const FLASH_MS = 1500;

/**
 * One stop per preset, evenly spaced by index; no number shown, the level name is tooltip and value text.
 * Moving the slider flashes the name of the selected translation for a moment.
 */
export function LevelSlider({ levels, value, onChange, label }: LevelSliderProps) {
  const id = useId();
  const [flash, setFlash] = useState<string | null>(null);
  const timer = useRef<ReturnType<typeof setTimeout> | undefined>(undefined);
  useEffect(() => () => clearTimeout(timer.current), []);
  if (levels.length < 2) return null;
  const found = levels.findIndex((l) => l.level === value);
  const index = Math.max(0, found);
  const name = levels[index]?.name;
  return (
    <div className="relative flex items-center">
      <input
        id={id}
        type="range"
        min={0}
        max={levels.length - 1}
        step={1}
        value={index}
        aria-label={label}
        aria-valuetext={name}
        title={name}
        onChange={(e) => {
          const next = levels[Number(e.target.value)];
          if (!next) return;
          onChange(next.level);
          setFlash(next.name);
          clearTimeout(timer.current);
          timer.current = setTimeout(() => setFlash(null), FLASH_MS);
        }}
        className="h-6 w-24 min-w-0 shrink cursor-pointer accent-accent focus-visible:outline-2 focus-visible:outline-focus sm:w-32"
      />
      {flash ? (
        <output className="pointer-events-none absolute right-0 top-full z-40 mt-1 whitespace-nowrap rounded-md border border-border bg-surface-muted px-2 py-1 text-sm shadow-sm">
          {flash}
        </output>
      ) : null}
    </div>
  );
}
