import { defineModule } from "@shared/moduleDefinition";
import { HousingPage } from "./HousingPage";
import en from "./locales/en.json";
import pl from "./locales/pl.json";

export const housingModule = defineModule({
  name: "Housing",
  routePrefix: "housing",
  namespace: "housing",
  locales: { pl, en },
  routes: [{ path: "", element: <HousingPage /> }],
});
