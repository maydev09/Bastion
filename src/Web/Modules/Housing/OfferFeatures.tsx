import { useTranslation } from "react-i18next";
import type { components } from "@shared/api/schema";
import { Panel } from "@shared/ui/Panel";
import { PropertyList } from "@shared/ui/PropertyList";
import { Tag } from "@shared/ui/Tag";
import styles from "./OfferDetail.module.css";
import { useFeatureGroups } from "./useFeatures";

type Offer = components["schemas"]["OfferListItem"];

// The amenities the advertiser listed, one row per category that has any.
export function OfferFeatures({ offer: o }: { offer: Offer }) {
  const { t } = useTranslation("housing");
  const listed = o.features ?? [];
  const rows = useFeatureGroups()
    .map((group) => ({
      category: group.category,
      features: group.features.filter((f) => listed.includes(f)),
    }))
    .filter((row) => row.features.length > 0);

  return (
    <Panel title={t("field.features")} flush={rows.length > 0}>
      {rows.length > 0 ? (
        <PropertyList
          items={rows.map((row) => ({
            label: t(`featureCategory.${row.category}`),
            value: (
              <span className={styles.tags}>
                {row.features.map((f) => (
                  <Tag key={f}>{t(`detail.feature.${f}`)}</Tag>
                ))}
              </span>
            ),
          }))}
        />
      ) : (
        <p className={styles.muted}>
          {o.detailsFetchedAt ? t("detail.noFeatures") : t("detail.notFetched")}
        </p>
      )}
    </Panel>
  );
}
