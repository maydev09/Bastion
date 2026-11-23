import { useTranslation } from "react-i18next";
import { ToggleGroup } from "@shared/ui/ToggleGroup";
import type { OfferFilter } from "./offerView";

type Props = {
  filter: OfferFilter;
  onChange: (filter: OfferFilter) => void;
  // When the user last looked at the offers; null on the first visit in this browser.
  previousVisit: string | null;
  // The moment 24 hours ago (useDayAgo), so the counts and the pick use the same one.
  dayAgo: string;
  // How many offers match the other filters since dayAgo and since previousVisit; undefined while loading.
  lastDay: number | undefined;
  sinceVisit: number | undefined;
};

const day = 24 * 60 * 60 * 1000;
const hour = 60 * 60 * 1000;

// Quick picks for offers Bastion found lately, each with how many of them match the other filters.
export function NewOffers({ filter, onChange, previousVisit, dayAgo, lastDay, sinceVisit }: Props) {
  const { t } = useTranslation("housing");

  // A "last 24 h" link opened later still counts as that pick for an hour; the chip shows the exact moment.
  const picked =
    filter.addedSince == null
      ? null
      : filter.addedSince === previousVisit
        ? "visit"
        : Math.abs(Date.parse(filter.addedSince) - Date.parse(dayAgo)) < hour
          ? "day"
          : null;
  const label = (text: string, count: number | undefined) =>
    count == null ? text : t("filters.withCount", { label: text, count });

  return (
    <ToggleGroup
      vertical
      label={t("filters.newOffers")}
      items={[
        { value: "day", label: label(t("filters.lastDay"), lastDay) },
        // On the first visit there is nothing to compare with, so the pick shows but cannot be used.
        previousVisit
          ? { value: "visit", label: label(t("filters.sinceVisit"), sinceVisit) }
          : { value: "visit", label: t("filters.sinceVisit"), disabled: true },
      ]}
      pressed={picked ? [picked] : []}
      onToggle={(value) =>
        onChange({
          ...filter,
          // "Last 24 h" counts back from the click, not from when the page opened.
          addedSince:
            value === picked
              ? null
              : value === "visit"
                ? previousVisit
                : new Date(Date.now() - day).toISOString(),
        })
      }
    />
  );
}
