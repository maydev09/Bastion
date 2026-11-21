import { Fragment } from "react";
import { useTranslation } from "react-i18next";
import type { components } from "@shared/api/schema";
import { Panel } from "@shared/ui/Panel";
import { PropertyList } from "@shared/ui/PropertyList";
import { useFormat } from "./useFormat";

type Offer = components["schemas"]["OfferListItem"];

// What the flat and its building are like; most of it comes from the detail page.
export function OfferDetails({ offer: o }: { offer: Offer }) {
  const { t } = useTranslation("housing");
  const format = useFormat();

  const floorName = o.floor == null ? null : format.floor(o.floor);
  const floor =
    floorName && o.buildingFloors
      ? t("detail.floorOf", { floor: floorName, floors: o.buildingFloors })
      : floorName;
  // Each feature is its own element, separated by commas.
  const features = (o.features ?? []).map((f, index) => (
    <Fragment key={f}>
      {index > 0 && ", "}
      <span>{t(`detail.feature.${f}`)}</span>
    </Fragment>
  ));

  return (
    <Panel title={t("detail.apartment")} flush>
      <PropertyList
        items={[
          { label: t("field.floor"), value: floor },
          {
            label: t("field.availableFrom"),
            value: o.availableFrom && format.day(o.availableFrom),
          },
          {
            label: t("field.buildingType"),
            value: o.buildingType && t(`detail.buildingType.${o.buildingType}`),
          },
          { label: t("field.buildYear"), value: o.buildYear },
          { label: t("field.heating"), value: o.heating && t(`detail.heating.${o.heating}`) },
          {
            label: t("field.condition"),
            value: o.condition && t(`detail.condition.${o.condition}`),
          },
          { label: t("field.features"), value: features.length > 0 ? features : null },
        ]}
      />
    </Panel>
  );
}
