import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { api } from "@shared/api/client";
import { toFilterQuery, type OfferFilter } from "./offerView";
import { offersQueryKey } from "./queryKeys";

// The typical values of the offers that match a filter (GET /api/housing/offers/summary).
// keepPrevious shows the last values while new ones load; off where old values would describe something else.
export function useSummary(filter: OfferFilter, enabled = true, keepPrevious = true) {
  return useQuery({
    queryKey: [...offersQueryKey, "summary", filter],
    queryFn: async () => {
      const { data, error } = await api.GET("/api/housing/offers/summary", {
        params: { query: toFilterQuery(filter) },
      });
      if (error) {
        throw new Error("summary");
      }
      return data;
    },
    placeholderData: keepPrevious ? keepPreviousData : undefined,
    enabled,
  });
}
