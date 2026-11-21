import { useId, type ReactNode } from "react";
import styles from "./Panel.module.css";

type Props = {
  title: string;
  // Content that sits at the panel's edges, such as a PropertyList or a table.
  flush?: boolean;
  children: ReactNode;
};

// A titled region of a page; screen readers can jump to it by its title.
export function Panel({ title, flush = false, children }: Props) {
  const titleId = useId();
  return (
    <section className={styles.panel} aria-labelledby={titleId}>
      <h2 id={titleId} className={styles.title}>
        {title}
      </h2>
      <div className={flush ? undefined : styles.body}>{children}</div>
    </section>
  );
}
