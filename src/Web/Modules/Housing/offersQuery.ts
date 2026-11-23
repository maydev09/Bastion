import { api } from "@shared/api/client";
import { toQuery, type OfferView } from "./offerView";
import { offersQueryKey } from "./queryKeys";

// One page of the offer list; the list and the offer card (its previous and next offer) share it.
export function offersQuery(view: OfferView) {
  return {
    queryKey: [...offersQueryKey, view],
    queryFn: async () => {
      const { data, error } = await api.GET("/api/housing/offers", {
        params: { query: toQuery(view) },
      });
      if (error) {
        throw new Error((error as { code?: string }).code ?? "");
      }
      return data;
    },
  };
}
