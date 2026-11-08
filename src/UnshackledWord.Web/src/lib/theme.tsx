import { createContext, type ReactNode, useContext, useEffect, useMemo, useState } from "react";

export type ThemePreference = "system" | "light" | "dark";

const storageKey = "uw-theme";
const preferences: readonly ThemePreference[] = ["system", "light", "dark"];

function readPreference(): ThemePreference {
  try {
    const value = localStorage.getItem(storageKey);
    return preferences.find((p) => p === value) ?? "system";
  } catch {
    return "system";
  }
}

function systemPrefersDark(): boolean {
  return window.matchMedia("(prefers-color-scheme: dark)").matches;
}

interface ThemeContextValue {
  preference: ThemePreference;
  setPreference: (preference: ThemePreference) => void;
}

const ThemeContext = createContext<ThemeContextValue | null>(null);

export function ThemeProvider({ children }: { children: ReactNode }) {
  const [preference, setPreference] = useState<ThemePreference>(readPreference);

  useEffect(() => {
    try {
      localStorage.setItem(storageKey, preference);
    } catch {
      // storage unavailable; the choice just does not persist
    }

    const apply = () => {
      const dark = preference === "dark" || (preference === "system" && systemPrefersDark());
      document.documentElement.dataset.theme = dark ? "dark" : "light";
    };
    apply();

    if (preference !== "system") return;
    const media = window.matchMedia("(prefers-color-scheme: dark)");
    media.addEventListener("change", apply);
    return () => media.removeEventListener("change", apply);
  }, [preference]);

  const value = useMemo(() => ({ preference, setPreference }), [preference]);
  return <ThemeContext.Provider value={value}>{children}</ThemeContext.Provider>;
}

export function useTheme(): ThemeContextValue {
  const context = useContext(ThemeContext);
  if (!context) throw new Error("useTheme must be used inside ThemeProvider");
  return context;
}

export { preferences as themePreferences };
