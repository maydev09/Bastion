import { useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { api } from "@shared/api/client";
import { ToggleGroup } from "@shared/ui/ToggleGroup";
import { toFilterQuery, type OfferFilter } from "./offerView";
import { offersQueryKey } from "./queryKeys";

type Props = {
  filter: OfferFilter;
  onChange: (filter: OfferFilter) => void;
  // When the user last looked at the offers; null on the first visit in this browser.
  previousVisit: string | null;
};

const day = 24 * 60 * 60 * 1000;
const hour = 60 * 60 * 1000;

// Quick picks for offers Bastion found lately, each with how many of them match the other filters.
export function NewOffers({ filter, onChange, previousVisit }: Props) {
  const { t } = useTranslation("housing");
  // Fixed when the page opens, so the count and the link do not move while the user looks at them.
  const [dayAgo] = useState(() => new Date(Date.now() - day).toISOString());
  const lastDay = useNewCount(filter, dayAgo);
  const sinceVisit = useNewCount(filter, previousVisit);

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
          addedSince: value === picked ? null : value === "visit" ? previousVisit : dayAgo,
        })
      }
    />
  );
}

// How many offers match the other filters and were found since the moment.
function useNewCount(filter: OfferFilter, since: string | null) {
  const query = useQuery({
    queryKey: [...offersQueryKey, "new", since, { ...filter, addedSince: null }],
    queryFn: async () => {
      const { data, error } = await api.GET("/api/housing/offers", {
        params: {
          query: { ...toFilterQuery(filter), addedSince: since ?? undefined, pageSize: 1 },
        },
      });
      if (error) {
        throw new Error("new offers");
      }
      return data.totalCount;
    },
    enabled: since != null,
  });
  return query.data;
}
