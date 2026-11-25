import { useQueries } from "@tanstack/react-query";
import { api } from "@shared/api/client";
import type { components } from "@shared/api/schema";
import { placesQueryKey } from "./queryKeys";

export type District = components["schemas"]["DistrictListItem"] & { placeId: number };

// The districts and neighborhoods of the picked places, most offers first; a neighborhood's parentId is its district.
// A place still loading adds none yet.
export function useDistricts(placeIds: number[]): District[] {
  const lists = useQueries({
    queries: placeIds.map((id) => ({
      queryKey: [...placesQueryKey, id, "districts"],
      queryFn: async () => {
        const { data } = await api.GET("/api/housing/places/{id}/districts", {
          params: { path: { id } },
        });
        return data ?? [];
      },
      staleTime: 5 * 60 * 1000,
    })),
  });
  return placeIds.flatMap((placeId, index) =>
    (lists[index]?.data ?? []).map((d) => ({ ...d, placeId })),
  );
}
