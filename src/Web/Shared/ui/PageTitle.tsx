import { useContext, type ReactNode } from "react";
import { createPortal } from "react-dom";
import { PageTitleSlot } from "../pageTitleSlot";
import styles from "./PageTitle.module.css";

type Props = {
  children: ReactNode;
  // Muted text after the title, e.g. how many rows the page shows.
  aside?: ReactNode;
  // Before the title, e.g. a button back to the list.
  before?: ReactNode;
};

// The page's heading, shown in the shell's top bar so the page itself keeps its space for data.
// A long title wraps and the bar grows. Outside the shell (e.g. in tests) it stays where the page renders it.
export function PageTitle({ children, aside, before }: Props) {
  const slot = useContext(PageTitleSlot);
  const title = (
    <div className={styles.title}>
      {before}
      <div className={styles.text}>
        <h1>{children}</h1>
        {aside != null && <span className={styles.aside}>{aside}</span>}
      </div>
    </div>
  );
  return slot ? createPortal(title, slot) : title;
}
