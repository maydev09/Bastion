import { useId, type ReactNode } from "react";
import styles from "./Panel.module.css";

type Props = {
  title: string;
  // Content that sits at the panel's edges, such as a PropertyList or a table.
  flush?: boolean;
  // Takes the height its flex container leaves and scrolls its content inside, e.g. a long text.
  fill?: boolean;
  children: ReactNode;
};

// A titled region of a page; screen readers can jump to it by its title.
export function Panel({ title, flush = false, fill = false, children }: Props) {
  const titleId = useId();
  return (
    <section
      className={fill ? `${styles.panel} ${styles.fill}` : styles.panel}
      aria-labelledby={titleId}
    >
      <h2 id={titleId} className={styles.title}>
        {title}
      </h2>
      <div
        className={[flush ? undefined : styles.body, fill ? styles.scroll : undefined].join(" ")}
      >
        {children}
      </div>
    </section>
  );
}
