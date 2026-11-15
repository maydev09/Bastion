import { flatOffersModule } from "@modules/FlatOffers/Web";
import { jobOffersModule } from "@modules/JobOffers/Web";
import type { ModuleDefinition } from "@shared/moduleDefinition";

export const allModules: ModuleDefinition[] = [flatOffersModule, jobOffersModule];
