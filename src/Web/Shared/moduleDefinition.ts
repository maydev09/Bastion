import type { ReactNode } from "react";

export type ModuleDefinition<Namespace extends string = string, Texts extends object = object> = {
  name: string;
  routePrefix: string;
  namespace: Namespace;
  locales: { pl: Texts; en: object };
  routes: { path: string; element: ReactNode }[];
};

export const defineModule = <const Namespace extends string, Texts extends object>(
  definition: ModuleDefinition<Namespace, Texts>,
) => definition;
