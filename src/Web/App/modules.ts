import { flatOffersModule } from "@modules/FlatOffers";
import { jobOffersModule } from "@modules/JobOffers";

export const allModules = [flatOffersModule, jobOffersModule] as const;
