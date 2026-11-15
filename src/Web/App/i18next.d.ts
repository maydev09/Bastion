import type common from "./locales/pl/common.json";
import type { allModules } from "./modules";

type ModuleTexts = {
  [Module in (typeof allModules)[number] as Module["namespace"]]: Module["locales"]["pl"];
};

declare module "i18next" {
  interface CustomTypeOptions {
    defaultNS: "common";
    resources: { common: typeof common } & ModuleTexts;
  }
}
