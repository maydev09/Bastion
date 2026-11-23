import { useQueries } from "@tanstack/react-query";
import { api } from "@shared/api/client";
import type { components } from "@shared/api/schema";
import { placesQueryKey } from "./queryKeys";

type Place = components["schemas"]["PlaceListItem"];

// The places of the filter, read by id (the link keeps only ids); null while one is loading or gone.
export function usePlaces(ids: number[]): (Place | null)[] {
  const places = useQueries({
    queries: ids.map((id) => ({
      queryKey: [...placesQueryKey, id],
      queryFn: async () => {
        const { data } = await api.GET("/api/housing/places/{id}", { params: { path: { id } } });
        return data ?? null;
      },
      staleTime: Infinity,
    })),
  });
  return places.map((place) => place.data ?? null);
}
