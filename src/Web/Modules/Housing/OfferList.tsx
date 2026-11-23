import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { Link, useLocation, useNavigate, useSearchParams } from "react-router";
import { api } from "@shared/api/client";
import type { components } from "@shared/api/schema";
import { Button } from "@shared/ui/Button";
import { DataTable, type DataTableColumn, type DataTableSort } from "@shared/ui/DataTable";
import { IconButton } from "@shared/ui/IconButton";
import { PageTitle } from "@shared/ui/PageTitle";
import { Pagination } from "@shared/ui/Pagination";
import { useErrorMessage } from "@shared/useErrorMessage";
import styles from "./Housing.module.css";
import { OfferFilters } from "./OfferFilters";
import {
  countFilters,
  noFilter,
  pageSizes,
  readView,
  sorts,
  toQuery,
  writeView,
  type OfferView,
} from "./offerView";
import { PlaceCell } from "./PlaceCell";
import { useFormat } from "./useFormat";
import { offersQueryKey } from "./queryKeys";

type Offer = components["schemas"]["OfferListItem"];

const noOffers: Offer[] = [];

export function OfferList() {
  const { t } = useTranslation("housing");
  const errorMessage = useErrorMessage();
  const format = useFormat();
  const navigate = useNavigate();
  const location = useLocation();
  const [params, setParams] = useSearchParams();
  const [showFilters, setShowFilters] = useState(true);
  // The offer the user opened last stays marked when they come back to the list.
  const [openedOffer] = useState(readOpenedOffer);
  const view = readView(params);
  const filterCount = countFilters(view.filter);

  const offers = useQuery({
    queryKey: [...offersQueryKey, view],
    queryFn: async () => {
      const { data, error } = await api.GET("/api/housing/offers", {
        params: { query: toQuery(view) },
      });
      if (error) {
        throw new Error((error as { code?: string }).code ?? "");
      }
      return data;
    },
    placeholderData: keepPreviousData,
  });

  const changeView = (changes: Partial<OfferView>) => setParams(writeView({ ...view, ...changes }));

  const columns = useMemo(
    (): DataTableColumn<Offer>[] => [
      {
        id: "title",
        header: t("list.offer"),
        minWidth: 260,
        cell: (offer) => <OfferLink offer={offer} />,
      },
      {
        id: "place",
        header: t("list.place"),
        minWidth: 240,
        cell: (offer) => <PlaceCell offer={offer} />,
      },
      {
        id: "Price",
        header: t("list.price"),
        align: "end",
        sortable: true,
        cell: (offer) =>
          offer.price != null && offer.priceCurrency
            ? format.money(offer.price, offer.priceCurrency)
            : "—",
      },
      {
        id: "PricePerM2",
        header: t("list.pricePerM2"),
        align: "end",
        sortable: true,
        cell: (offer) =>
          offer.pricePerM2 != null && offer.priceCurrency
            ? format.wholeMoney(offer.pricePerM2, offer.priceCurrency)
            : "—",
      },
      {
        id: "AreaM2",
        header: t("list.area"),
        align: "end",
        sortable: true,
        cell: (offer) => (offer.areaM2 != null ? format.area(offer.areaM2) : "—"),
      },
      {
        id: "Rooms",
        header: t("list.rooms"),
        align: "end",
        sortable: true,
        cell: (offer) => offer.rooms ?? "—",
      },
      {
        id: "FirstSeen",
        header: t("list.firstSeen"),
        align: "end",
        sortable: true,
        descendingFirst: true,
        cell: (offer) =>
          offer.externalCreatedAt ? format.shortDate(offer.externalCreatedAt) : "—",
      },
    ],
    [t, format],
  );

  const sort: DataTableSort = { column: view.sort, descending: view.direction === "Descending" };

  const renderOffers = () => {
    if (offers.isPending) {
      return <p className={styles.muted}>{t("list.loading")}</p>;
    }
    if (offers.isError) {
      return (
        <p className={styles.error} role="alert">
          {errorMessage(offers.error.message)}
        </p>
      );
    }

    const { items, totalCount } = offers.data;
    if (totalCount === 0) {
      return filterCount > 0 ? (
        <div className={styles.empty}>
          <p>{t("filters.noMatch")}</p>
          <Button onClick={() => changeView({ page: 1, filter: noFilter })}>
            {t("filters.clear")}
          </Button>
        </div>
      ) : (
        <div className={styles.empty}>
          <p>{t("list.empty")}</p>
          <Link to="../collection" relative="path">
            {t("list.goToCollection")}
          </Link>
        </div>
      );
    }

    return (
      <>
        <DataTable
          columns={columns}
          rows={items.length > 0 ? items : noOffers}
          getRowId={(offer) => String(offer.id)}
          sort={sort}
          onSortChange={(next) =>
            changeView({
              page: 1,
              sort: sorts.find((s) => s === next.column) ?? "FirstSeen",
              direction: next.descending ? "Descending" : "Ascending",
            })
          }
          onRowClick={(offer) => {
            rememberOpenedOffer(offer.id);
            void navigate(String(offer.id), { relative: "path", state: { from: location.search } });
          }}
          busy={offers.isPlaceholderData}
          fill
          // Each view of the list is its own history entry, so back restores how far it was scrolled.
          scrollKey={location.key}
          markedRowId={openedOffer ?? undefined}
        />
        {items.length === 0 && <p className={styles.empty}>{t("list.pageEmpty")}</p>}
        <Pagination
          page={view.page}
          pageSize={view.pageSize}
          totalCount={totalCount}
          pageSizes={pageSizes}
          onPageChange={(page) => changeView({ page })}
          // The first offer of the current page stays on the new page.
          onPageSizeChange={(pageSize) =>
            changeView({
              pageSize,
              page: Math.floor(((view.page - 1) * view.pageSize) / pageSize) + 1,
            })
          }
        />
      </>
    );
  };

  return (
    <section className={styles.list}>
      <PageTitle aside={offers.data && t("list.count", { count: offers.data.totalCount })}>
        {t("list.title")}
      </PageTitle>
      <div className={styles.body} data-filters={showFilters ? "open" : "closed"}>
        {showFilters ? (
          <div className={styles.filters}>
            {/* A changed filter starts from the first page. */}
            <OfferFilters
              filter={view.filter}
              onChange={(filter) => changeView({ page: 1, filter })}
              onHide={() => setShowFilters(false)}
            />
          </div>
        ) : (
          // Hidden, the filters leave a strip with their button and how many are in use.
          <div className={styles.filtersStrip}>
            <IconButton
              icon="filter"
              label={t("filters.show")}
              aria-expanded={false}
              onClick={() => setShowFilters(true)}
            />
            {filterCount > 0 && (
              <span className={styles.badge} title={t("filters.inUse", { count: filterCount })}>
                {filterCount}
              </span>
            )}
          </div>
        )}
        <div className={styles.offers}>{renderOffers()}</div>
      </div>
    </section>
  );
}

// The offer card links back to the list view the user came from.
function OfferLink({ offer }: { offer: Offer }) {
  const location = useLocation();
  return (
    <Link
      to={String(offer.id)}
      relative="path"
      state={{ from: location.search }}
      className={styles.title}
      onClick={() => rememberOpenedOffer(offer.id)}
    >
      {offer.title}
    </Link>
  );
}

// Kept for the browser tab, so the mark also survives a reload; storage may be unavailable (private mode).
const openedOfferKey = "housing.openedOffer";

function readOpenedOffer(): string | null {
  try {
    return sessionStorage.getItem(openedOfferKey);
  } catch {
    return null;
  }
}

function rememberOpenedOffer(id: number) {
  try {
    sessionStorage.setItem(openedOfferKey, String(id));
  } catch {
    // Without storage the list simply marks nothing.
  }
}
