import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { useMemo } from "react";
import { useTranslation } from "react-i18next";
import { Link, useLocation, useNavigate, useSearchParams } from "react-router";
import { api } from "@shared/api/client";
import type { components } from "@shared/api/schema";
import { DataTable, type DataTableColumn, type DataTableSort } from "@shared/ui/DataTable";
import { Pagination } from "@shared/ui/Pagination";
import { useErrorMessage } from "@shared/useErrorMessage";
import styles from "./Housing.module.css";
import { PlaceCell } from "./PlaceCell";
import { useFormat } from "./useFormat";
import { offersQueryKey } from "./queryKeys";

type Offer = components["schemas"]["OfferListItem"];
type OfferSort = components["schemas"]["OfferSort"];

const sorts: OfferSort[] = ["FirstSeen", "Price", "PricePerM2", "AreaM2", "Rooms"];
const pageSizes = [25, 50, 100];
const defaultPageSize = 50;
const noOffers: Offer[] = [];

// The page, page size and sort order live in the URL, so a copied link opens the same view.
function readView(params: URLSearchParams) {
  const page = Number.parseInt(params.get("page") ?? "", 10);
  const pageSize = Number.parseInt(params.get("pageSize") ?? "", 10);
  const sort = sorts.find((s) => s === params.get("sort")) ?? "FirstSeen";
  return {
    page: page >= 1 ? page : 1,
    pageSize: pageSizes.includes(pageSize) ? pageSize : defaultPageSize,
    sort,
    direction: params.get("direction") === "Ascending" ? "Ascending" : "Descending",
  } as const;
}

export function OfferList() {
  const { t } = useTranslation("housing");
  const errorMessage = useErrorMessage();
  const format = useFormat();
  const navigate = useNavigate();
  const location = useLocation();
  const [params, setParams] = useSearchParams();
  const view = readView(params);

  const offers = useQuery({
    queryKey: [...offersQueryKey, view],
    queryFn: async () => {
      const { data, error } = await api.GET("/api/housing/offers", {
        params: { query: view },
      });
      if (error) {
        throw new Error((error as { code?: string }).code ?? "");
      }
      return data;
    },
    placeholderData: keepPreviousData,
  });

  const changeView = (changes: Partial<typeof view>) => {
    const next = { ...view, ...changes };
    // Default values stay out of the URL, so the plain list has a plain link.
    const query: Record<string, string> = {};
    if (next.page > 1) {
      query.page = String(next.page);
    }
    if (next.pageSize !== defaultPageSize) {
      query.pageSize = String(next.pageSize);
    }
    if (next.sort !== "FirstSeen") {
      query.sort = next.sort;
    }
    if (next.direction !== "Descending") {
      query.direction = next.direction;
    }
    setParams(query);
  };

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
  const sort: DataTableSort = { column: view.sort, descending: view.direction === "Descending" };

  return (
    <section className={styles.list}>
      <div className={styles.toolbar}>
        <h2>{t("list.title")}</h2>
        <span className={styles.count}>{t("list.count", { count: totalCount })}</span>
      </div>
      {totalCount === 0 ? (
        <p className={styles.empty}>{t("list.empty")}</p>
      ) : (
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
            onRowClick={(offer) =>
              void navigate(`offers/${offer.id}`, { state: { from: location.search } })
            }
            busy={offers.isPlaceholderData}
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
      )}
    </section>
  );
}

// The offer card links back to the list view the user came from.
function OfferLink({ offer }: { offer: Offer }) {
  const location = useLocation();
  return (
    <Link to={`offers/${offer.id}`} state={{ from: location.search }} className={styles.title}>
      {offer.title}
    </Link>
  );
}
