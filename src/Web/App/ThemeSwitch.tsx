import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { IconButton } from "@shared/ui/IconButton";
import { applyTheme, readTheme, themeKey, themes } from "./theme";

const icons = { system: "system", light: "sun", dark: "moon" } as const;

// Each click picks the next theme: system, light, dark, then system again.
export function ThemeSwitch() {
  const { t } = useTranslation();
  const [theme, setTheme] = useState(readTheme);

  useEffect(() => {
    applyTheme(theme);
    try {
      localStorage.setItem(themeKey, theme);
    } catch {
      // Without storage the choice lasts until the page is closed.
    }
  }, [theme]);

  return (
    <IconButton
      icon={icons[theme]}
      label={t("theme.label", { theme: t(`theme.${theme}`) })}
      onClick={() => setTheme(themes[(themes.indexOf(theme) + 1) % themes.length])}
    />
  );
}
