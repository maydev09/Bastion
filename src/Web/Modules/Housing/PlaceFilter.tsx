import { useQuery } from "@tanstack/react-query";
import { useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { api } from "@shared/api/client";
import type { components } from "@shared/api/schema";
import { MultiSelect } from "@shared/ui/MultiSelect";
import { placesQueryKey } from "./queryKeys";
import { usePlaces } from "./usePlaces";

type Place = components["schemas"]["PlaceListItem"];

const typingPause = 250;

type Props = {
  value: number[];
  onChange: (ids: number[]) => void;
};

// Places are searched on the server; the link keeps only their ids, so the picked ones are read by id.
export function PlaceFilter({ value, onChange }: Props) {
  const { t } = useTranslation("housing");
  const [search, setSearch] = useState("");
  const timer = useRef<ReturnType<typeof setTimeout>>(undefined);
  useEffect(() => () => clearTimeout(timer.current), []);

  const results = useQuery({
    queryKey: [...placesQueryKey, "search", search],
    queryFn: async () => {
      const { data } = await api.GET("/api/housing/places", { params: { query: { search } } });
      return data ?? [];
    },
    enabled: search.length > 0,
  });
  const picked = usePlaces(value);
  // A place still loading (or gone) shows as its id until its name is known.
  const pickedPlaces = value.map(
    (id, index): Place =>
      picked[index] ?? {
        id,
        name: `#${id}`,
        voivodeship: null,
        county: null,
        municipality: null,
        kind: null,
        offerCount: 0,
      },
  );

  const emptyText =
    search.length === 0
      ? t("filters.placeStart")
      : results.isFetching
        ? t("filters.searching")
        : t("filters.placeNone");

  return (
    <MultiSelect
      label={t("filters.place")}
      placeholder={t("filters.placePlaceholder")}
      items={search.length > 0 ? (results.data ?? []) : []}
      value={pickedPlaces}
      onChange={(places) => onChange(places.map((p) => p.id))}
      itemToKey={(place) => String(place.id)}
      itemToLabel={(place) => place.name}
      itemToHint={(place) =>
        [
          place.county
            ? t("filters.inCounty", { county: place.county })
            : place.voivodeship && t(`voivodeship.${place.voivodeship}`),
          t("list.count", { count: place.offerCount }),
        ]
          .filter(Boolean)
          .join(" · ")
      }
      // A place without offers is found too, greyed out, so the user knows the name is right.
      isItemMuted={(place) => place.offerCount === 0}
      onSearch={(text) => {
        clearTimeout(timer.current);
        timer.current = setTimeout(() => setSearch(text.trim()), typingPause);
      }}
      emptyText={emptyText}
      removeLabel={(name) => t("filters.remove", { name })}
    />
  );
}
