import { useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { NavLink, Route, Routes, useLocation } from "react-router";
import { api } from "@shared/api/client";
import { ModuleTitle } from "@shared/ModuleTitle";
import { readModuleText } from "@shared/moduleText";
import { PageTitleSlot } from "@shared/pageTitleSlot";
import { Icon, type IconName } from "@shared/ui/Icon";
import { IconButton } from "@shared/ui/IconButton";
import { Select } from "@shared/ui/Select";
import { HomePage } from "./HomePage";
import { languages } from "./i18n";
import { allModules } from "./modules";
import styles from "./Shell.module.css";
import { ThemeSwitch } from "./ThemeSwitch";

export function Shell() {
  const { t, i18n } = useTranslation();
  const [sidebarOpen, setSidebarOpen] = useState(readSidebarOpen);
  const { pathname } = useLocation();
  const [shownPath, setShownPath] = useState(pathname);
  const [titleSlot, setTitleSlot] = useState<HTMLElement | null>(null);
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

  // The menu starts as the user left it with its button (open the first time); going to another page
  // hides it, so the page gets the whole width. The menu button shows it again.
  if (pathname !== shownPath) {
    setShownPath(pathname);
    setSidebarOpen(false);
  }

  return (
    <div className={styles.layout} data-sidebar={sidebarOpen ? "open" : "closed"}>
      <aside className={styles.sidebar}>
        <div className={styles.brand}>
          <IconButton
            icon="menu"
            label={t("nav.toggle")}
            aria-controls="navigation"
            aria-expanded={sidebarOpen}
            onClick={() => {
              setSidebarOpen(!sidebarOpen);
              rememberSidebarOpen(!sidebarOpen);
            }}
          />
          <span hidden={!sidebarOpen}>{t("app.title")}</span>
        </div>
        <nav id="navigation" className={styles.nav}>
          <NavItem to="/" end icon="dashboard" label={t("nav.home")} collapsed={!sidebarOpen} />
          {enabled.data?.map((m) =>
            m.pages ? (
              <div key={m.name} className={styles.group}>
                <span
                  className={styles.groupTitle}
                  title={sidebarOpen ? undefined : readModuleText(i18n, m.namespace)}
                >
                  <Icon name={m.icon} />
                  <span className={styles.label}>{readModuleText(i18n, m.namespace)}</span>
                </span>
                <div className={styles.groupPages}>
                  {m.pages.map((p) => (
                    <NavItem
                      key={p.path}
                      to={`/${m.routePrefix}/${p.path}`}
                      icon={p.icon}
                      label={readModuleText(i18n, m.namespace, p.title)}
                      collapsed={!sidebarOpen}
                    />
                  ))}
                </div>
              </div>
            ) : (
              <NavItem
                key={m.name}
                to={`/${m.routePrefix}`}
                icon={m.icon}
                label={readModuleText(i18n, m.namespace)}
                collapsed={!sidebarOpen}
              />
            ),
          )}
          {import.meta.env.DEV &&
            enabled.data &&
            allModules
              .filter((m) => !enabled.data.some((e) => e.name === m.name))
              .map((m) => (
                <span
                  key={m.name}
                  className={styles.disabled}
                  aria-disabled="true"
                  title={t("nav.disabledHint")}
                >
                  <Icon name={m.icon} />
                  <span className={styles.label}>
                    <ModuleTitle namespace={m.namespace} />
                  </span>
                  <small className={styles.label}>{t("nav.disabled")}</small>
                </span>
              ))}
        </nav>
      </aside>
      <div className={styles.page}>
        <header className={styles.topbar}>
          <div ref={setTitleSlot} className={styles.title} />
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
        <PageTitleSlot value={titleSlot}>
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
        </PageTitleSlot>
      </div>
    </div>
  );
}

type NavItemProps = {
  to: string;
  end?: boolean;
  icon: IconName;
  label: string;
  collapsed: boolean;
};

// Collapsed, the menu shows only the icon; the label stays for screen readers and shows on hover.
function NavItem({ to, end, icon, label, collapsed }: NavItemProps) {
  return (
    <NavLink to={to} end={end} title={collapsed ? label : undefined}>
      <Icon name={icon} />
      <span className={styles.label}>{label}</span>
    </NavLink>
  );
}

const sidebarKey = "shell.sidebarOpen";

function readSidebarOpen(): boolean {
  try {
    return localStorage.getItem(sidebarKey) !== "false";
  } catch {
    return true;
  }
}

function rememberSidebarOpen(open: boolean) {
  try {
    localStorage.setItem(sidebarKey, String(open));
  } catch {
    // Without storage the menu starts open next time.
  }
}
