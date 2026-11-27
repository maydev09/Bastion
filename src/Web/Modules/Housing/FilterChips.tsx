import { useTranslation } from "react-i18next";
import { Button } from "@shared/ui/Button";
import { Tag } from "@shared/ui/Tag";
import styles from "./Housing.module.css";
import { noFilter, type OfferFilter } from "./offerView";
import { useDistricts } from "./useDistricts";
import { usePlaces } from "./usePlaces";
import { useFormat } from "./useFormat";

type Props = {
  filter: OfferFilter;
  onChange: (filter: OfferFilter) => void;
};

type Chip = { key: string; label: string; remove: Partial<OfferFilter> };

// The filters in use above the list, each removable on its own, so they stay in view with the panel hidden.
export function FilterChips({ filter, onChange }: Props) {
  const { t } = useTranslation("housing");
  const format = useFormat();
  const places = usePlaces(filter.place);
  const districts = useDistricts(filter.place);
  const districtOf = (id: number) => districts.find((d) => d.id === id);

  const range = (
    what: string,
    min: number | null,
    max: number | null,
    show: (n: number) => string,
  ) =>
    min != null && max != null
      ? t("filters.chipRange", { what, from: show(min), to: show(max) })
      : min != null
        ? t("filters.chipFrom", { what, value: show(min) })
        : t("filters.chipTo", { what, value: show(max ?? 0) });
  // The price bounds of the filter are in PLN.
  const money = (value: number) => format.wholeMoney(value, "PLN");

  const chips: Chip[] = [
    ...filter.place.map((id, index) => ({
      key: `place-${id}`,
      // A place still loading shows as an ellipsis.
      label: places[index]?.name ?? "…",
      // A place takes its districts and neighborhoods with it.
      remove: {
        place: filter.place.filter((p) => p !== id),
        district: filter.district.filter((d) => districtOf(d)?.placeId !== id),
        neighborhood: filter.neighborhood.filter((e) => districtOf(e)?.placeId !== id),
      },
    })),
    ...filter.district.map((id) => ({
      key: `district-${id}`,
      label: districtOf(id)?.name ?? "…",
      // A district takes its neighborhoods with it.
      remove: {
        district: filter.district.filter((d) => d !== id),
        neighborhood: filter.neighborhood.filter((e) => districtOf(e)?.parentId !== id),
      },
    })),
    ...filter.neighborhood.map((id) => ({
      key: `neighborhood-${id}`,
      label: districtOf(id)?.name ?? "…",
      remove: { neighborhood: filter.neighborhood.filter((e) => e !== id) },
    })),
    ...(filter.withoutPlace
      ? [{ key: "withoutPlace", label: t("filters.withoutPlace"), remove: { withoutPlace: false } }]
      : []),
    ...filter.voivodeship.map((v) => ({
      key: `voivodeship-${v}`,
      label: t(`voivodeship.${v}`),
      remove: { voivodeship: filter.voivodeship.filter((x) => x !== v) },
    })),
    ...filter.size.map((size) => ({
      key: `size-${size}`,
      label: t(`place.size.${size}`),
      remove: { size: filter.size.filter((s) => s !== size) },
    })),
    ...filter.role.map((role) => ({
      key: `role-${role}`,
      label: t(`place.role.${role}`),
      remove: { role: filter.role.filter((r) => r !== role) },
    })),
  ];
  if (filter.priceMin != null || filter.priceMax != null) {
    chips.push({
      key: "price",
      label: range(t("filters.price"), filter.priceMin, filter.priceMax, money),
      remove: { priceMin: null, priceMax: null },
    });
  }
  if (filter.totalCostMin != null || filter.totalCostMax != null) {
    chips.push({
      key: "totalCost",
      label: range(t("filters.totalCost"), filter.totalCostMin, filter.totalCostMax, money),
      remove: { totalCostMin: null, totalCostMax: null },
    });
  }
  if (filter.pricePerM2Min != null || filter.pricePerM2Max != null) {
    chips.push({
      key: "pricePerM2",
      label: range(t("filters.pricePerM2"), filter.pricePerM2Min, filter.pricePerM2Max, (value) =>
        format.perM2(value, "PLN"),
      ),
      remove: { pricePerM2Min: null, pricePerM2Max: null },
    });
  }
  if (filter.areaMin != null || filter.areaMax != null) {
    chips.push({
      key: "area",
      label: range(t("filters.area"), filter.areaMin, filter.areaMax, format.area),
      remove: { areaMin: null, areaMax: null },
    });
  }
  if (filter.rooms.length > 0 || filter.roomsMin != null) {
    const picked = [...filter.rooms].sort((a, b) => a - b).map(String);
    if (filter.roomsMin != null) {
      picked.push(t("filters.orMore", { count: filter.roomsMin }));
    }
    chips.push({
      key: "rooms",
      label: t("filters.chipValue", { what: t("filters.rooms"), value: picked.join(", ") }),
      remove: { rooms: [], roomsMin: null },
    });
  }
  if (filter.furnished) {
    chips.push({
      key: "furnished",
      label: t("filters.chipValue", {
        what: t("field.furnished"),
        value: t(`answer.${filter.furnished}`),
      }),
      remove: { furnished: null },
    });
  }
  chips.push(
    ...filter.feature.map((feature) => ({
      key: `feature-${feature}`,
      label: t(`detail.feature.${feature}`),
      remove: { feature: filter.feature.filter((f) => f !== feature) },
    })),
  );
  if (filter.addedSince) {
    chips.push({
      key: "addedSince",
      label: t("filters.chipValue", {
        what: t("filters.addedSince"),
        value: format.date(filter.addedSince),
      }),
      remove: { addedSince: null },
    });
  }
  if (filter.changed != null) {
    chips.push({
      key: "changed",
      label: t(filter.changed ? "filters.changedYes" : "filters.changedNo"),
      remove: { changed: null },
    });
  }
  if (filter.priceCut) {
    chips.push({ key: "priceCut", label: t("filters.priceCut"), remove: { priceCut: false } });
  }
  if (filter.detailsFetched != null) {
    chips.push({
      key: "detailsFetched",
      label: t(filter.detailsFetched ? "filters.detailsYes" : "filters.detailsNo"),
      remove: { detailsFetched: null },
    });
  }
  if (filter.excludedFromStatistics != null) {
    chips.push({
      key: "excludedFromStatistics",
      label: t(filter.excludedFromStatistics ? "filters.excludedYes" : "filters.excludedNo"),
      remove: { excludedFromStatistics: null },
    });
  }

  if (chips.length === 0) {
    return null;
  }
  return (
    <div className={styles.chips} role="group" aria-label={t("filters.inUseList")}>
      {chips.map((chip) => (
        <Tag
          key={chip.key}
          onRemove={() => onChange({ ...filter, ...chip.remove })}
          removeLabel={t("filters.remove", { name: chip.label })}
        >
          {chip.label}
        </Tag>
      ))}
      {chips.length >= 2 && (
        <Button variant="ghost" size="sm" onClick={() => onChange(noFilter)}>
          {t("filters.clear")}
        </Button>
      )}
    </div>
  );
}
