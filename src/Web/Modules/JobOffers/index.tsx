import { defineModule } from "@shared/moduleDefinition";
import { JobOffersPage } from "./JobOffersPage";
import en from "./locales/en.json";
import pl from "./locales/pl.json";

export const jobOffersModule = defineModule({
  name: "JobOffers",
  routePrefix: "job-offers",
  namespace: "jobOffers",
  locales: { pl, en },
  routes: [{ path: "", element: <JobOffersPage /> }],
});
