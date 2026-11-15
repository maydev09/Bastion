import i18n from "i18next";
import LanguageDetector from "i18next-browser-languagedetector";
import { initReactI18next } from "react-i18next";
import { allModules } from "./modules";
import enCommon from "./locales/en/common.json";
import plCommon from "./locales/pl/common.json";

export const languages = ["pl", "en"] as const;

const resources = {
  pl: {
    common: plCommon,
    ...Object.fromEntries(allModules.map((m) => [m.namespace, m.locales.pl])),
  },
  en: {
    common: enCommon,
    ...Object.fromEntries(allModules.map((m) => [m.namespace, m.locales.en])),
  },
};

void i18n
  .use(LanguageDetector)
  .use(initReactI18next)
  .init({
    resources,
    supportedLngs: languages,
    fallbackLng: "pl",
    defaultNS: "common",
    interpolation: { escapeValue: false },
    detection: { order: ["localStorage", "navigator"], caches: ["localStorage"] },
  });

export default i18n;
