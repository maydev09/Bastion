import { housingModule } from "@modules/Housing";
import { jobOffersModule } from "@modules/JobOffers";

export const allModules = [housingModule, jobOffersModule] as const;
