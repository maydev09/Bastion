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
  return (
    <Panel title={t("detail.apartment")} flush>
      <PropertyList
        items={[
          { label: t("field.floor"), value: floor },
          {
            label: t("field.availableFrom"),
            value: o.availableFrom && format.day(o.availableFrom),
          },
          // Empty (a dash) while the detail page is not fetched; "not stated" when the page says nothing.
          { label: t("field.furnished"), value: o.furnished && t(`answer.${o.furnished}`) },
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
        ]}
      />
    </Panel>
  );
}
