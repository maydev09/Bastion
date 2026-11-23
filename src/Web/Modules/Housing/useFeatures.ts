import { useQuery } from "@tanstack/react-query";
import { api } from "@shared/api/client";
import type { components } from "@shared/api/schema";
import { featuresQueryKey } from "./queryKeys";

type Feature = components["schemas"]["Feature"];
type FeatureCategory = components["schemas"]["FeatureCategory"];

export type FeatureGroup = { category: FeatureCategory; features: Feature[] };

// Every amenity grouped by category, in display order; the backend owns the groups, so all portals share them.
export function useFeatureGroups() {
  const query = useQuery({
    queryKey: featuresQueryKey,
    queryFn: async () => {
      const { data, error } = await api.GET("/api/housing/features");
      if (error) {
        throw new Error("features");
      }
      return data;
    },
    staleTime: Infinity,
  });

  const groups: FeatureGroup[] = [];
  for (const item of query.data ?? []) {
    const group = groups.find((g) => g.category === item.category);
    if (group) {
      group.features.push(item.feature);
    } else {
      groups.push({ category: item.category, features: [item.feature] });
    }
  }
  return groups;
}
