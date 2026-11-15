import type { ReactNode } from "react";

export type ModuleDefinition = {
  name: string;
  routePrefix: string;
  namespace: string;
  locales: { pl: object; en: object };
  routes: { path: string; element: ReactNode }[];
};
