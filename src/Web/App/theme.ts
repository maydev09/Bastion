export const themeKey = "theme";

export const themes = ["system", "light", "dark"] as const;
export type Theme = (typeof themes)[number];

// Reads the remembered theme; the page also applies it before the first render (main.tsx).
export function readTheme(): Theme {
  try {
    return themes.find((t) => t === localStorage.getItem(themeKey)) ?? "system";
  } catch {
    return "system";
  }
}

// "system" removes data-theme, so the colors follow the operating system again.
export function applyTheme(theme: Theme) {
  if (theme === "system") {
    delete document.documentElement.dataset.theme;
  } else {
    document.documentElement.dataset.theme = theme;
  }
}
