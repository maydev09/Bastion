import { useTranslation } from "react-i18next";
import type { components } from "@shared/api/schema";
import styles from "./OfferDetail.module.css";
import { useFormat } from "./useFormat";

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

export function OfferDetails({ offer: o }: { offer: Offer }) {
  const { t } = useTranslation("housing");
  const format = useFormat();

  if (!o.detailsFetchedAt) {
    return (
      <section className={styles.card} aria-labelledby="details">
        <h2 id="details">{t("detail.details")}</h2>
        <p className={styles.muted}>{t("detail.notFetched")}</p>
      </section>
    );
  }

  const building = [
    o.buildingType && t(`detail.buildingType.${o.buildingType}`),
    o.buildYear && t("detail.buildYear", { year: o.buildYear }),
    o.heating && t(`detail.heating.${o.heating}`),
    o.condition && t(`detail.condition.${o.condition}`),
  ].filter(Boolean);
  const map =
    o.latitude != null && o.longitude != null
      ? `https://www.openstreetmap.org/?mlat=${o.latitude}&mlon=${o.longitude}#map=16/${o.latitude}/${o.longitude}`
      : null;
  const description = o.description ? plainText(o.description) : "";

  return (
    <section className={styles.card} aria-labelledby="details">
      <h2 id="details">{t("detail.details")}</h2>
      {building.length > 0 && <p>{building.join(" · ")}</p>}
      {o.features && o.features.length > 0 && (
        <p className={styles.badges}>
          {o.features.map((f) => (
            <span key={f} className={styles.badge}>
              {t(`detail.feature.${f}`)}
            </span>
          ))}
        </p>
      )}
      {map && (
        <p>
          <a href={map} target="_blank" rel="noreferrer">
            {t("detail.map")} ↗
          </a>{" "}
          {o.locationRadiusM ? (
            <span className={styles.muted}>
              {t("detail.mapApproximate", {
                km: format.number(Math.max(o.locationRadiusM / 1000, 0.1)),
              })}
            </span>
          ) : null}
        </p>
      )}
      {description && <p className={styles.description}>{description}</p>}
    </section>
  );
}
