import type { TFunction } from "i18next";
import { useMemo } from "react";
import { useTranslation } from "react-i18next";

// Numbers, money, dates and distances in the user's language, the same in the list and on the offer card.
// The result changes only with the language, so tables can keep their columns between renders.
// Times show in the time zone of the device (the browser's), whatever the language: the API sends
// moments with their offset, so "14:35" is 14:35 where the user is. The language only picks the format.
export function useFormat() {
  const { t, i18n } = useTranslation("housing");
  const language = i18n.resolvedLanguage ?? "pl";
  return useMemo(() => createFormat(t, language), [t, language]);
}

function createFormat(t: TFunction<"housing">, language: string) {
  const number = new Intl.NumberFormat(language);
  const whole = new Intl.NumberFormat(language, { maximumFractionDigits: 0 });
  const dateTime = new Intl.DateTimeFormat(language, { dateStyle: "medium", timeStyle: "short" });
  const dateOnly = new Intl.DateTimeFormat(language, { dateStyle: "medium" });
  const shortDateOnly = new Intl.DateTimeFormat(language, { dateStyle: "short" });
  const timeOnly = new Intl.DateTimeFormat(language, { timeStyle: "short" });
  const shortDateTime = new Intl.DateTimeFormat(language, {
    dateStyle: "short",
    timeStyle: "short",
  });
  const relative = new Intl.RelativeTimeFormat(language, { numeric: "auto", style: "short" });

  return {
    number: (value: number) => number.format(value),
    // "2 200 zł", but "2 200,50 zł" when there are grosze.
    money: (value: number, currency: string) =>
      new Intl.NumberFormat(language, {
        style: "currency",
        currency,
        trailingZeroDisplay: "stripIfInteger",
      }).format(value),
    // "45,50 zł/m²" in the price per m² filter and its chip.
    perM2: (value: number, currency: string) =>
      t("units.perM2", {
        value: new Intl.NumberFormat(language, {
          style: "currency",
          currency,
          trailingZeroDisplay: "stripIfInteger",
        }).format(value),
      }),
    wholeMoney: (value: number, currency: string) =>
      new Intl.NumberFormat(language, {
        style: "currency",
        currency,
        maximumFractionDigits: 0,
      }).format(value),
    date: (iso: string) => dateTime.format(new Date(iso)),
    // "5 min temu", "2 godz. temu" within a day of now (the server's time, so a wrong device clock does not matter),
    // else "21.11.2026, 11:45".
    ago: (iso: string, now: string) => {
      const seconds = Math.round((Date.parse(iso) - Date.parse(now)) / 1000);
      if (seconds > -60) {
        return relative.format(0, "second");
      }
      if (seconds > -3600) {
        return relative.format(Math.round(seconds / 60), "minute");
      }
      if (seconds > -86400) {
        return relative.format(Math.round(seconds / 3600), "hour");
      }
      return shortDateTime.format(new Date(iso));
    },
    // "21.11.2026, 11:45" for table columns.
    shortDate: (iso: string) => shortDateTime.format(new Date(iso)),
    // "21.11.2026" and "11:45" for narrow columns, one under the other.
    shortDay: (iso: string) => shortDateOnly.format(new Date(iso)),
    time: (iso: string) => timeOnly.format(new Date(iso)),
    // A calendar day ("2026-12-01") without a time zone.
    day: (iso: string) => dateOnly.format(new Date(`${iso}T00:00`)),
    area: (m2: number) => t("units.area", { value: number.format(m2) }),
    km: (km: number) => whole.format(km),
    floor: (floor: number) =>
      floor === 0 ? t("detail.groundFloor") : floor < 0 ? t("detail.basement") : String(floor),
  };
}
