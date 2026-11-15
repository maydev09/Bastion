import type common from "./locales/pl/common.json";
import type flatOffers from "@modules/FlatOffers/locales/pl.json";
import type jobOffers from "@modules/JobOffers/locales/pl.json";

declare module "i18next" {
  interface CustomTypeOptions {
    defaultNS: "common";
    resources: {
      common: typeof common;
      flatOffers: typeof flatOffers;
      jobOffers: typeof jobOffers;
    };
  }
}
