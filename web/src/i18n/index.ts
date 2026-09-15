import i18next from "i18next";
import { initReactI18next } from "react-i18next";
import en from "./locales/en.json";
import tr from "./locales/tr.json";

export const LANGUAGES = [
  { code: "en", label: "English" },
  { code: "tr", label: "Türkçe" },
] as const;

export type LanguageCode = (typeof LANGUAGES)[number]["code"];

/** The language to start in when the user hasn't chosen one: their browser's, if we speak it. */
export function detectLanguage(): LanguageCode {
  const preferred = typeof navigator !== "undefined" ? navigator.languages ?? [navigator.language] : [];
  for (const tag of preferred) {
    const base = tag?.split("-")[0]?.toLowerCase();
    if (LANGUAGES.some((l) => l.code === base)) return base as LanguageCode;
  }
  return "en";
}

/** English is the source language: its file is the one kept complete, and anything missing from
 * another locale falls back to it rather than showing a raw key. */
export function initI18n(language: LanguageCode) {
  void i18next.use(initReactI18next).init({
    resources: { en: { translation: en }, tr: { translation: tr } },
    lng: language,
    fallbackLng: "en",
    interpolation: { escapeValue: false }, // React escapes for us
  });
  return i18next;
}

export { i18next };
