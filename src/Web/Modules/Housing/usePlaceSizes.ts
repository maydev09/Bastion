import { useQuery } from "@tanstack/react-query";
import { api } from "@shared/api/client";
import type { components } from "@shared/api/schema";
import { placeSizesQueryKey } from "./queryKeys";

type PlaceSize = NonNullable<components["schemas"]["PlaceSize"]>;
type PlaceSizeListItem = components["schemas"]["PlaceSizeListItem"];

// The populations of each place size under the thresholds the backend is configured with;
// empty until they load, so the sizes show without them.
export function usePlaceSizes(): Partial<Record<PlaceSize, PlaceSizeListItem>> {
  const query = useQuery({
    queryKey: placeSizesQueryKey,
    queryFn: async () => {
      const { data, error } = await api.GET("/api/housing/place-sizes");
      if (error) {
        throw new Error("placeSizes");
      }
      return data;
    },
    staleTime: Infinity,
  });
  return Object.fromEntries((query.data ?? []).map((item) => [item.size, item]));
}
