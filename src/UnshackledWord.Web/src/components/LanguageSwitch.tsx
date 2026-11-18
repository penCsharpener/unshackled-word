import { useTranslation } from "react-i18next";
import { type Language, languages, setLanguage } from "@/i18n";
import { Button } from "./Button";

/** One button that toggles between the languages; it shows the current one (DE / EN). */
export function LanguageSwitch() {
  const { t, i18n } = useTranslation();
  const current = (i18n.language as Language) ?? languages[0];
  const next = languages[(languages.indexOf(current) + 1) % languages.length] ?? languages[0];
  const label = `${t("language.label")}: ${t(`language.${current}`)}`;

  return (
    <Button
      variant="ghost"
      size="sm"
      aria-label={label}
      title={label}
      onClick={() => setLanguage(next)}
    >
      {current.toUpperCase()}
    </Button>
  );
}
