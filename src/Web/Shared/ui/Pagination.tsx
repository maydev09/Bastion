import { useTranslation } from "react-i18next";
import { IconButton } from "./IconButton";
import styles from "./Pagination.module.css";
import { Select } from "./Select";

type Props = {
  page: number;
  pageSize: number;
  totalCount: number;
  pageSizes: number[];
  onPageChange: (page: number) => void;
  onPageSizeChange: (pageSize: number) => void;
};

export function Pagination({
  page,
  pageSize,
  totalCount,
  pageSizes,
  onPageChange,
  onPageSizeChange,
}: Props) {
  const { t, i18n } = useTranslation();
  const number = new Intl.NumberFormat(i18n.resolvedLanguage);
  const pageCount = Math.max(1, Math.ceil(totalCount / pageSize));
  const from = Math.min((page - 1) * pageSize + 1, totalCount);
  const to = Math.min(page * pageSize, totalCount);

  // The typed page is applied on Enter or when the field loses focus; an invalid one is undone.
  const goTo = (input: HTMLInputElement) => {
    const typed = Number.parseInt(input.value, 10);
    const target = Number.isNaN(typed) ? page : Math.min(Math.max(typed, 1), pageCount);
    input.value = String(target);
    if (target !== page) {
      onPageChange(target);
    }
  };

  return (
    <nav className={styles.pagination} aria-label={t("table.pagination")}>
      <span className={styles.range}>
        {t("table.range", {
          from: number.format(from),
          to: number.format(to),
          total: number.format(totalCount),
        })}
      </span>
      <span className={styles.pageSize}>
        {t("table.pageSize")}
        <Select
          label={t("table.pageSize")}
          value={pageSize}
          items={pageSizes.map((size) => ({ value: size, label: String(size) }))}
          onChange={onPageSizeChange}
        />
      </span>
      <span className={styles.pages}>
        <IconButton
          icon="first"
          label={t("table.first")}
          disabled={page <= 1}
          onClick={() => onPageChange(1)}
        />
        <IconButton
          icon="previous"
          label={t("table.previous")}
          disabled={page <= 1}
          onClick={() => onPageChange(Math.min(page - 1, pageCount))}
        />
        <span className={styles.position}>
          <input
            key={page}
            className={styles.input}
            aria-label={t("table.page")}
            inputMode="numeric"
            defaultValue={page}
            onKeyDown={(e) => e.key === "Enter" && goTo(e.currentTarget)}
            onBlur={(e) => goTo(e.currentTarget)}
          />
          {t("table.of", { pages: number.format(pageCount) })}
        </span>
        <IconButton
          icon="next"
          label={t("table.next")}
          disabled={page >= pageCount}
          onClick={() => onPageChange(page + 1)}
        />
        <IconButton
          icon="last"
          label={t("table.last")}
          disabled={page >= pageCount}
          onClick={() => onPageChange(pageCount)}
        />
      </span>
    </nav>
  );
}
