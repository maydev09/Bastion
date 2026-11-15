import { flatOffersModule } from "@modules/FlatOffers";
import { jobOffersModule } from "@modules/JobOffers";
import type { ModuleDefinition } from "@shared/moduleDefinition";

export const allModules: ModuleDefinition[] = [flatOffersModule, jobOffersModule];
