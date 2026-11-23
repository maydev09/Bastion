import type { HTMLAttributes } from "react";
import styles from "./VisuallyHidden.module.css";

// Text only for screen readers, e.g. "Loading offers…" or what a chart shows.
export function VisuallyHidden(props: HTMLAttributes<HTMLSpanElement>) {
  return <span {...props} className={styles.hidden} />;
}
