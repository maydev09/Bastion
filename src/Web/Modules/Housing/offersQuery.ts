import { api } from "@shared/api/client";
import { toQuery, type OfferView } from "./offerView";
import { offersQueryKey } from "./queryKeys";

// One page of the offer list; the list and the offer card (its previous and next offer) share it.
// Without a view (a card not opened from a list) the query does not run.
export function offersQuery(view: OfferView | null) {
  return {
    queryKey: [...offersQueryKey, view],
    queryFn: async () => {
      if (view === null) {
        throw new Error("no list");
      }
      const { data, error } = await api.GET("/api/housing/offers", {
        params: { query: toQuery(view) },
      });
      if (error) {
        throw new Error((error as { code?: string }).code ?? "");
      }
      return data;
    },
    enabled: view !== null,
  };
}
