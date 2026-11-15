import type { ModuleDefinition } from "@shared/moduleDefinition";
import { FlatOffersPage } from "./FlatOffersPage";
import en from "./locales/en.json";
import pl from "./locales/pl.json";

export const flatOffersModule: ModuleDefinition = {
  name: "FlatOffers",
  routePrefix: "flat-offers",
  namespace: "flatOffers",
  locales: { pl, en },
  routes: [{ path: "", element: <FlatOffersPage /> }],
};
