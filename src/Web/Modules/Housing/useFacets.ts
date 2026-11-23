import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { api } from "@shared/api/client";
import { toFilterQuery, type OfferFilter } from "./offerView";
import { offersQueryKey } from "./queryKeys";

// How many offers each filter choice would show; undefined until the first counts arrive.
export function useFacets(filter: OfferFilter) {
  const query = useQuery({
    queryKey: [...offersQueryKey, "facets", filter],
    queryFn: async () => {
      const { data, error } = await api.GET("/api/housing/offers/facets", {
        params: { query: toFilterQuery(filter) },
      });
      if (error) {
        throw new Error("facets");
      }
      return data;
    },
    placeholderData: keepPreviousData,
  });
  return query.data;
}
