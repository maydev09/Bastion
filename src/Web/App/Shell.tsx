import { useQuery } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { NavLink, Route, Routes } from "react-router";
import { api } from "@shared/api/client";
import { ModuleTitle } from "@shared/ModuleTitle";
import { IconButton } from "@shared/ui/IconButton";
import { Select } from "@shared/ui/Select";
import { HomePage } from "./HomePage";
import { languages } from "./i18n";
import { allModules } from "./modules";
import styles from "./Shell.module.css";
import { ThemeSwitch } from "./ThemeSwitch";

const sidebarKey = "sidebarOpen";

export function Shell() {
  const { t, i18n } = useTranslation();
  const [sidebarOpen, setSidebarOpen] = useState(
    () => localStorage.getItem(sidebarKey) !== "false",
  );
  const enabled = useQuery({
    queryKey: ["platform", "modules"],
    queryFn: async () => {
      const { data, error } = await api.GET("/api/platform/modules");
      if (error) {
        throw new Error("modules");
      }
      const names = new Set(data.map((m) => m.name));
      return allModules.filter((m) => names.has(m.name));
    },
  });

  useEffect(() => localStorage.setItem(sidebarKey, String(sidebarOpen)), [sidebarOpen]);

  return (
    <div className={styles.layout} data-sidebar={sidebarOpen ? "open" : "closed"}>
      <aside id="sidebar" className={styles.sidebar} inert={!sidebarOpen}>
        <div className={styles.brand}>{t("app.title")}</div>
        <nav className={styles.nav}>
          <NavLink to="/" end>
            {t("nav.home")}
          </NavLink>
          {enabled.data?.map((m) => (
            <NavLink key={m.name} to={`/${m.routePrefix}`}>
              <ModuleTitle namespace={m.namespace} />
            </NavLink>
          ))}
          {import.meta.env.DEV &&
            enabled.data &&
            allModules
              .filter((m) => !enabled.data.includes(m))
              .map((m) => (
                <span
                  key={m.name}
                  className={styles.disabled}
                  aria-disabled="true"
                  title={t("nav.disabledHint")}
                >
                  <ModuleTitle namespace={m.namespace} />
                  <small>{t("nav.disabled")}</small>
                </span>
              ))}
        </nav>
      </aside>
      <div className={styles.page}>
        <header className={styles.topbar}>
          <IconButton
            icon="menu"
            label={t("nav.toggle")}
            aria-controls="sidebar"
            aria-expanded={sidebarOpen}
            onClick={() => setSidebarOpen((open) => !open)}
          />
          <div className={styles.actions}>
            <ThemeSwitch />
            <Select
              label={t("language.label")}
              value={i18n.resolvedLanguage ?? "pl"}
              items={languages.map((l) => ({ value: l, label: l.toUpperCase() }))}
              onChange={(language) => void i18n.changeLanguage(language)}
            />
          </div>
        </header>
        <main className={styles.main}>
          {enabled.isPending && <p>{t("status.loading")}</p>}
          {enabled.isError && <p role="alert">{t("status.modulesFailed")}</p>}
          {enabled.data && (
            <Routes>
              <Route index element={<HomePage modules={enabled.data} />} />
              {enabled.data.map((m) => (
                <Route key={m.name} path={m.routePrefix}>
                  {m.routes.map((r) => (
                    <Route
                      key={r.path}
                      path={r.path || undefined}
                      index={!r.path}
                      element={r.element}
                    />
                  ))}
                </Route>
              ))}
              <Route path="*" element={<p>{t("status.notFound")}</p>} />
            </Routes>
          )}
        </main>
      </div>
    </div>
  );
}
