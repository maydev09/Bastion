import { createContext } from "react";

// The place in the shell's top bar where a page puts its title (see ui/PageTitle).
export const PageTitleSlot = createContext<HTMLElement | null>(null);
