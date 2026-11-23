import type { ReactNode } from "react";
import type { IconName } from "./ui/Icon";

export type ModuleDefinition<Namespace extends string = string, Texts extends object = object> = {
  name: string;
  routePrefix: string;
  // Shown in the menu, alone when the menu is collapsed.
  icon: IconName;
  namespace: Namespace;
  locales: { pl: Texts; en: object };
  routes: { path: string; element: ReactNode }[];
  // Pages listed in the menu under the module's title; title is a key in the module's namespace.
  // Without them the module's title itself links to the module.
  pages?: { path: string; title: string; icon: IconName }[];
};

export const defineModule = <const Namespace extends string, Texts extends object>(
  definition: ModuleDefinition<Namespace, Texts>,
) => definition;
