import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { api } from "@shared/api/client";
import { toFilterQuery, type OfferFilter } from "./offerView";
import { offersQueryKey } from "./queryKeys";

// How many offers each filter choice would show, and how many Bastion found since each of the moments
// (e.g. 24 hours ago, the last visit); undefined until the first counts arrive.
export function useFacets(filter: OfferFilter, countAddedSince: string[]) {
  const query = useQuery({
    queryKey: [...offersQueryKey, "facets", filter, countAddedSince],
    queryFn: async () => {
      const { data, error } = await api.GET("/api/housing/offers/facets", {
        params: { query: { ...toFilterQuery(filter), countAddedSince } },
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
