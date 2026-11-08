import { useTranslation } from "react-i18next";
import { type Language, languages, setLanguage } from "@/i18n";
import { Button } from "./Button";

export function LanguageSwitch() {
  const { t, i18n } = useTranslation();

  return (
    <fieldset className="flex gap-1">
      <legend className="sr-only">{t("language.label")}</legend>
      {languages.map((language: Language) => (
        <Button
          key={language}
          size="sm"
          variant={i18n.language === language ? "primary" : "ghost"}
          aria-pressed={i18n.language === language}
          onClick={() => setLanguage(language)}
        >
          {language.toUpperCase()}
        </Button>
      ))}
    </fieldset>
  );
}
