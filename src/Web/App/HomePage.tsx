import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import { ModuleTitle } from "@shared/ModuleTitle";
import { PageTitle } from "@shared/ui/PageTitle";
import type { ModuleDefinition } from "@shared/moduleDefinition";
import { DatabaseStatus } from "./DatabaseStatus";

export function HomePage({ modules }: { modules: ModuleDefinition[] }) {
  const { t } = useTranslation();

  return (
    <>
      <PageTitle>{t("home.title")}</PageTitle>
      <ul>
        {modules.map((m) => (
          <li key={m.name}>
            <Link to={`/${m.routePrefix}`}>
              <ModuleTitle namespace={m.namespace} />
            </Link>
          </li>
        ))}
      </ul>
      <DatabaseStatus />
    </>
  );
}
