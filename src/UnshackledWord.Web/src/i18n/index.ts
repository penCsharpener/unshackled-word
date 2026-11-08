import i18n from "i18next";
import { initReactI18next } from "react-i18next";
import { de } from "./de";
import { en } from "./en";

export const languages = ["de", "en"] as const;
export type Language = (typeof languages)[number];

const storageKey = "uw-language";

function storedLanguage(): Language {
  try {
    const value = localStorage.getItem(storageKey);
    return languages.find((l) => l === value) ?? "de";
  } catch {
    return "de";
  }
}

export function setLanguage(language: Language) {
  try {
    localStorage.setItem(storageKey, language);
  } catch {
    // storage unavailable (private mode); the choice just does not persist
  }
  void i18n.changeLanguage(language);
}

i18n.use(initReactI18next).init({
  resources: { de: { translation: de }, en: { translation: en } },
  lng: storedLanguage(),
  fallbackLng: "de",
  interpolation: { escapeValue: false },
});

i18n.on("languageChanged", (language) => {
  document.documentElement.lang = language;
});
document.documentElement.lang = i18n.language;

export default i18n;
