import { useTranslation } from "react-i18next";
import { type ThemePreference, themePreferences, useTheme } from "@/lib/theme";
import { Button } from "./Button";

const ICONS: Record<ThemePreference, string[]> = {
  system: ["M3 5h18v11H3z", "M8 20h8", "M12 16v4"],
  light: [
    "M12 8a4 4 0 1 0 0 8 4 4 0 0 0 0-8Z",
    "M12 2v2M12 20v2M4.9 4.9l1.4 1.4M17.7 17.7l1.4 1.4M2 12h2M20 12h2M4.9 19.1l1.4-1.4M17.7 6.3l1.4-1.4",
  ],
  dark: ["M21 12.8A9 9 0 1 1 11.2 3 7 7 0 0 0 21 12.8Z"],
};

/** One icon button that cycles system → light → dark; the icon shows the current preference. */
export function ThemeToggle() {
  const { t } = useTranslation();
  const { preference, setPreference } = useTheme();
  const next =
    themePreferences[(themePreferences.indexOf(preference) + 1) % themePreferences.length] ??
    "system";
  const label = `${t("theme.label")}: ${t(`theme.${preference}`)}`;

  return (
    <Button
      variant="ghost"
      size="sm"
      aria-label={label}
      title={label}
      onClick={() => setPreference(next)}
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
        {ICONS[preference].map((d) => (
          <path key={d} d={d} />
        ))}
      </svg>
    </Button>
  );
}
