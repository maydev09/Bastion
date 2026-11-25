import { useTranslation } from "react-i18next";
import { MultiSelect } from "@shared/ui/MultiSelect";
import type { District } from "./useDistricts";
import { useFormat } from "./useFormat";

type Props = {
  // The districts and neighborhoods of the picked places (useDistricts).
  districts: District[];
  // The names of the picked places, to tell their districts apart when there are several.
  placeNames: Map<number, string>;
  district: number[];
  neighborhood: number[];
  onChange: (picked: { district: number[]; neighborhood: number[] }) => void;
};

// Shown once a picked place has districts: a picked district narrows its place, a picked neighborhood its district.
// The neighborhoods are those of the picked districts, or of all districts while none is picked.
export function DistrictFilter({ districts, placeNames, district, neighborhood, onChange }: Props) {
  const { t } = useTranslation("housing");
  const format = useFormat();
  if (districts.length === 0) {
    return null;
  }

  const names = new Map(districts.map((d) => [d.id, d.name]));
  const districtItems = districts.filter((d) => d.parentId == null);
  const neighborhoodItems = districts.filter(
    (d) =>
      d.parentId != null &&
      (district.length === 0 || district.includes(d.parentId) || neighborhood.includes(d.id)),
  );
  const severalPlaces = new Set(districts.map((d) => d.placeId)).size > 1;
  const severalDistricts = new Set(neighborhoodItems.map((d) => d.parentId)).size > 1;
  const common = {
    placeholder: t("filters.districtPlaceholder"),
    itemToKey: (d: District) => String(d.id),
    itemToLabel: (d: District) => d.name,
    itemToCount: (d: District) => format.number(d.offerCount),
    isItemMuted: (d: District) => d.offerCount === 0,
    removeLabel: (name: string) => t("filters.remove", { name }),
  };

  return (
    <>
      <MultiSelect
        {...common}
        label={t("filters.district")}
        items={districtItems}
        value={districtItems.filter((d) => district.includes(d.id))}
        // A district taken out of the filter takes its neighborhoods with it.
        onChange={(picked) => {
          const ids = picked.map((d) => d.id);
          const removed = district.filter((id) => !ids.includes(id));
          onChange({
            district: ids,
            neighborhood: neighborhood.filter((id) => {
              const parentId = districts.find((d) => d.id === id)?.parentId;
              return parentId == null || !removed.includes(parentId);
            }),
          });
        }}
        itemToHint={(d) => (severalPlaces ? (placeNames.get(d.placeId) ?? "") : "")}
        emptyText={t("filters.districtNone")}
      />
      {neighborhoodItems.length > 0 && (
        <MultiSelect
          {...common}
          label={t("filters.neighborhood")}
          items={neighborhoodItems}
          value={neighborhoodItems.filter((d) => neighborhood.includes(d.id))}
          onChange={(picked) => onChange({ district, neighborhood: picked.map((d) => d.id) })}
          itemToHint={(d) => (severalDistricts ? (names.get(d.parentId ?? 0) ?? "") : "")}
          emptyText={t("filters.neighborhoodNone")}
        />
      )}
    </>
  );
}
