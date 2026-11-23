import type { components } from "@shared/api/schema";

type Voivodeship = NonNullable<components["schemas"]["Voivodeship"]>;
type PlaceSize = NonNullable<components["schemas"]["PlaceSize"]>;
type PlaceRole = NonNullable<components["schemas"]["PlaceRole"]>;
type Feature = components["schemas"]["Feature"];
type OfferSort = components["schemas"]["OfferSort"];
type Answer = NonNullable<components["schemas"]["Answer"]>;
type FeatureMatch = NonNullable<components["schemas"]["FeatureMatch"]>;

export const voivodeships: Voivodeship[] = [
  "Dolnoslaskie",
  "KujawskoPomorskie",
  "Lubelskie",
  "Lubuskie",
  "Lodzkie",
  "Malopolskie",
  "Mazowieckie",
  "Opolskie",
  "Podkarpackie",
  "Podlaskie",
  "Pomorskie",
  "Slaskie",
  "Swietokrzyskie",
  "WarminskoMazurskie",
  "Wielkopolskie",
  "Zachodniopomorskie",
];
export const townSizes: PlaceSize[] = [
  "Metropolis",
  "VeryLargeCity",
  "LargeCity",
  "LargerTown",
  "MediumTown",
  "SmallTown",
  "VerySmallTown",
];
export const villageSizes: PlaceSize[] = [
  "VeryLargeVillage",
  "LargeVillage",
  "MediumVillage",
  "SmallVillage",
];
// The highest seat first, as on the offer card.
export const placeRoles: PlaceRole[] = ["VoivodeshipCapital", "CountySeat", "MunicipalitySeat"];
export const answers: Answer[] = ["Yes", "No", "NotStated"];
export const sorts: OfferSort[] = ["FirstSeen", "Price", "Rent", "PricePerM2", "AreaM2", "Rooms"];
// The room counts picked one by one; from this one on the pick means "or more" (roomsMin).
export const roomCounts = [1, 2, 3];
export const roomsOrMore = 4;
export const pageSizes = [25, 50, 100];
const defaultPageSize = 50;

// The URL keys are the API's query parameters, so a list link reads like the request behind it.
export type OfferFilter = {
  voivodeship: Voivodeship[];
  place: number[];
  // Offers whose location matched no place; joined with OR with the places and voivodeships.
  withoutPlace: boolean;
  size: PlaceSize[];
  // Places whose highest seat is any of them.
  role: PlaceRole[];
  feature: Feature[];
  // Offers with all the picked amenities, or with any of them.
  featureMatch: FeatureMatch;
  priceMin: number | null;
  priceMax: number | null;
  pricePerM2Min: number | null;
  pricePerM2Max: number | null;
  areaMin: number | null;
  areaMax: number | null;
  // Offers with any of the room counts, or with at least roomsMin.
  rooms: number[];
  roomsMin: number | null;
  furnished: Answer | null;
  // Offers Bastion first saw at or after this moment (ISO), e.g. since the last visit.
  addedSince: string | null;
  // With (true) or without (false) a recorded change.
  changed: boolean | null;
  priceCut: boolean;
  // Leaves one offer out, e.g. the offer compared with the others; never part of a list link.
  excludeOffer: number | null;
};

export type OfferView = {
  page: number;
  pageSize: number;
  sort: OfferSort;
  direction: "Ascending" | "Descending";
  filter: OfferFilter;
};

export const noFilter: OfferFilter = {
  voivodeship: [],
  place: [],
  withoutPlace: false,
  size: [],
  role: [],
  feature: [],
  featureMatch: "All",
  priceMin: null,
  priceMax: null,
  pricePerM2Min: null,
  pricePerM2Max: null,
  areaMin: null,
  areaMax: null,
  rooms: [],
  roomsMin: null,
  furnished: null,
  addedSince: null,
  changed: null,
  priceCut: false,
  excludeOffer: null,
};

const numberKeys = [
  "priceMin",
  "priceMax",
  "pricePerM2Min",
  "pricePerM2Max",
  "areaMin",
  "areaMax",
  "roomsMin",
] as const;

// Unknown or broken values in a link are ignored, so an old link still opens a list.
export function readView(params: URLSearchParams): OfferView {
  const page = Number.parseInt(params.get("page") ?? "", 10);
  const pageSize = Number.parseInt(params.get("pageSize") ?? "", 10);
  const filter: OfferFilter = {
    ...noFilter,
    voivodeship: voivodeships.filter((v) => params.getAll("voivodeship").includes(v)),
    place: params
      .getAll("place")
      .map(Number)
      .filter((id) => Number.isInteger(id) && id > 0),
    withoutPlace: params.get("withoutPlace") === "true",
    size: [...townSizes, ...villageSizes].filter((s) => params.getAll("size").includes(s)),
    role: placeRoles.filter((r) => params.getAll("role").includes(r)),
    rooms: roomCounts.filter((r) => params.getAll("rooms").includes(String(r))),
    // The amenities come from the API; one it does not know makes the list fail, so only names pass here.
    feature: params.getAll("feature").filter((f): f is Feature => /^[A-Za-z]+$/.test(f)),
    featureMatch: params.get("featureMatch") === "Any" ? "Any" : "All",
    furnished: answers.find((a) => a === params.get("furnished")) ?? null,
    addedSince: readMoment(params.get("addedSince")),
    changed:
      params.get("changed") === "true" ? true : params.get("changed") === "false" ? false : null,
    priceCut: params.get("priceCut") === "true",
  };
  for (const key of numberKeys) {
    const value = params.get(key);
    filter[key] = value && Number.isFinite(Number(value)) ? Number(value) : null;
  }
  return {
    page: page >= 1 ? page : 1,
    pageSize: pageSizes.includes(pageSize) ? pageSize : defaultPageSize,
    sort: sorts.find((s) => s === params.get("sort")) ?? "FirstSeen",
    direction: params.get("direction") === "Ascending" ? "Ascending" : "Descending",
    filter,
  };
}

// Default values stay out of the URL, so the plain list has a plain link.
export function writeView(view: OfferView): URLSearchParams {
  const params = new URLSearchParams();
  const { filter } = view;
  filter.voivodeship.forEach((v) => params.append("voivodeship", v));
  filter.place.forEach((id) => params.append("place", String(id)));
  if (filter.withoutPlace) {
    params.set("withoutPlace", "true");
  }
  filter.size.forEach((s) => params.append("size", s));
  filter.role.forEach((r) => params.append("role", r));
  filter.rooms.forEach((r) => params.append("rooms", String(r)));
  filter.feature.forEach((f) => params.append("feature", f));
  if (filter.featureMatch !== "All") {
    params.set("featureMatch", filter.featureMatch);
  }
  for (const key of numberKeys) {
    const value = filter[key];
    if (value != null) {
      params.set(key, String(value));
    }
  }
  if (filter.furnished) {
    params.set("furnished", filter.furnished);
  }
  if (filter.addedSince) {
    params.set("addedSince", filter.addedSince);
  }
  if (filter.changed != null) {
    params.set("changed", String(filter.changed));
  }
  if (filter.priceCut) {
    params.set("priceCut", "true");
  }
  if (view.page > 1) {
    params.set("page", String(view.page));
  }
  if (view.pageSize !== defaultPageSize) {
    params.set("pageSize", String(view.pageSize));
  }
  if (view.sort !== "FirstSeen") {
    params.set("sort", view.sort);
  }
  if (view.direction !== "Descending") {
    params.set("direction", view.direction);
  }
  return params;
}

// The query of GET /api/housing/offers.
export function toQuery(view: OfferView) {
  return {
    page: view.page,
    pageSize: view.pageSize,
    sort: view.sort,
    direction: view.direction,
    ...toFilterQuery(view.filter),
  };
}

// The filters of the offer list and of its counts; filters not in use are left out.
export function toFilterQuery(filter: OfferFilter) {
  const list = <T>(values: T[]) => (values.length > 0 ? values : undefined);
  return {
    voivodeship: list(filter.voivodeship),
    place: list(filter.place),
    withoutPlace: filter.withoutPlace || undefined,
    size: list(filter.size),
    role: list(filter.role),
    feature: list(filter.feature),
    featureMatch: filter.featureMatch === "All" ? undefined : filter.featureMatch,
    priceMin: filter.priceMin ?? undefined,
    priceMax: filter.priceMax ?? undefined,
    pricePerM2Min: filter.pricePerM2Min ?? undefined,
    pricePerM2Max: filter.pricePerM2Max ?? undefined,
    areaMin: filter.areaMin ?? undefined,
    areaMax: filter.areaMax ?? undefined,
    rooms: list(filter.rooms),
    roomsMin: filter.roomsMin ?? undefined,
    furnished: filter.furnished ?? undefined,
    addedSince: filter.addedSince ?? undefined,
    changed: filter.changed ?? undefined,
    priceCut: filter.priceCut || undefined,
    excludeOffer: filter.excludeOffer ?? undefined,
  };
}

// Every picked value counts: two voivodeships and a top price are three filters.
export function countFilters(filter: OfferFilter): number {
  return (
    filter.voivodeship.length +
    filter.place.length +
    (filter.withoutPlace ? 1 : 0) +
    filter.size.length +
    filter.role.length +
    filter.rooms.length +
    filter.feature.length +
    numberKeys.filter((key) => filter[key] != null).length +
    (filter.furnished ? 1 : 0) +
    (filter.addedSince ? 1 : 0) +
    (filter.changed != null ? 1 : 0) +
    (filter.priceCut ? 1 : 0)
  );
}

// A moment from a link, kept as written when it is a valid date.
function readMoment(value: string | null): string | null {
  return value && !Number.isNaN(Date.parse(value)) ? value : null;
}
