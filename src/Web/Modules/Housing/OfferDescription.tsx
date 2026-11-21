import { useTranslation } from "react-i18next";
import type { components } from "@shared/api/schema";
import { Panel } from "@shared/ui/Panel";
import styles from "./OfferDetail.module.css";

type Offer = components["schemas"]["OfferListItem"];

// The description is the advertiser's HTML; it is shown as plain text, never as markup.
function plainText(html: string) {
  const document = new DOMParser().parseFromString(
    html.replace(/<br\s*\/?>|<\/(p|li|div|h\d)>/gi, "$&\n"),
    "text/html",
  );
  // textContent would include the code of scripts and styles.
  document.querySelectorAll("script, style").forEach((element) => element.remove());
  return (document.body.textContent ?? "").replace(/\n{3,}/g, "\n\n").trim();
}

export function OfferDescription({ offer: o }: { offer: Offer }) {
  const { t } = useTranslation("housing");
  const description = o.description ? plainText(o.description) : "";

  return (
    <Panel title={t("detail.description")}>
      {!o.detailsFetchedAt ? (
        <p className={styles.muted}>{t("detail.notFetched")}</p>
      ) : description ? (
        <p className={styles.description}>{description}</p>
      ) : (
        <p className={styles.muted}>{t("detail.noDescription")}</p>
      )}
    </Panel>
  );
}
