import { useTranslation } from "react-i18next";
import { themePreferences, useTheme } from "@/lib/theme";
import { Button } from "./Button";

export function ThemeToggle() {
  const { t } = useTranslation();
  const { preference, setPreference } = useTheme();

  return (
    <fieldset className="flex gap-1">
      <legend className="sr-only">{t("theme.label")}</legend>
      {themePreferences.map((option) => (
        <Button
          key={option}
          size="sm"
          variant={preference === option ? "primary" : "ghost"}
          aria-pressed={preference === option}
          onClick={() => setPreference(option)}
        >
          {t(`theme.${option}`)}
        </Button>
      ))}
    </fieldset>
  );
}
