import type { ReactNode } from "react";
import styles from "./PropertyList.module.css";

type Props = {
  // An empty value shows as a dash, so every record has the same rows.
  items: { label: string; value: ReactNode }[];
};

export function PropertyList({ items }: Props) {
  return (
    <dl className={styles.list}>
      {items.map((item) => (
        <div key={item.label} className={styles.row}>
          <dt>{item.label}</dt>
          <dd>{item.value == null || item.value === "" ? "—" : item.value}</dd>
        </div>
      ))}
    </dl>
  );
}
