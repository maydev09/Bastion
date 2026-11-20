import { useTranslation } from "react-i18next";

// Numbers, money, dates and distances in the user's language, the same in the list and on the offer card.
export function useFormat() {
  const { t, i18n } = useTranslation("housing");
  const language = i18n.resolvedLanguage ?? "pl";
  const number = new Intl.NumberFormat(language);
  const whole = new Intl.NumberFormat(language, { maximumFractionDigits: 0 });
  const dateTime = new Intl.DateTimeFormat(language, { dateStyle: "medium", timeStyle: "short" });
  const dateOnly = new Intl.DateTimeFormat(language, { dateStyle: "medium" });

  return {
    number: (value: number) => number.format(value),
    money: (value: number, currency: string) =>
      new Intl.NumberFormat(language, { style: "currency", currency }).format(value),
    date: (iso: string) => dateTime.format(new Date(iso)),
    // A calendar day ("2026-12-01") without a time zone.
    day: (iso: string) => dateOnly.format(new Date(`${iso}T00:00`)),
    area: (m2: number) => t("units.area", { value: number.format(m2) }),
    km: (km: number) => whole.format(km),
  };
}
