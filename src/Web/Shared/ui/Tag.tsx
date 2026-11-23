import type { ReactNode } from "react";
import styles from "./Tag.module.css";

// A short label in a pill, e.g. one amenity of an offer.
export function Tag({ children }: { children: ReactNode }) {
  return <span className={styles.tag}>{children}</span>;
}
