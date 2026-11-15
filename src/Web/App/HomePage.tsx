import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import { ModuleTitle } from "@shared/ModuleTitle";
import type { ModuleDefinition } from "@shared/moduleDefinition";

export function HomePage({ modules }: { modules: ModuleDefinition[] }) {
  const { t } = useTranslation();

  return (
    <>
      <h1>{t("home.title")}</h1>
      <ul>
        {modules.map((m) => (
          <li key={m.name}>
            <Link to={`/${m.routePrefix}`}>
              <ModuleTitle namespace={m.namespace} />
            </Link>
          </li>
        ))}
      </ul>
    </>
  );
}
