import { Collapsible as BaseCollapsible } from "@base-ui/react/collapsible";
import { useId, type ReactNode } from "react";
import styles from "./Collapsible.module.css";
import { Icon } from "./Icon";

type Props = {
  title: string;
  // A short line next to the title, visible while the panel is closed.
  hint?: string;
  defaultOpen?: boolean;
  // Content that sits at the edges, such as a table.
  flush?: boolean;
  children: ReactNode;
};

export function Collapsible({ title, hint, defaultOpen = false, flush = false, children }: Props) {
  const hintId = useId();
  return (
    <BaseCollapsible.Root className={styles.root} defaultOpen={defaultOpen}>
      <BaseCollapsible.Trigger
        className={styles.trigger}
        aria-label={title}
        aria-describedby={hint && hintId}
      >
        <span className={styles.chevron}>
          <Icon name="next" size={16} />
        </span>
        <span className={styles.title}>{title}</span>
        {hint && (
          <span id={hintId} className={styles.hint}>
            {hint}
          </span>
        )}
      </BaseCollapsible.Trigger>
      <BaseCollapsible.Panel className={flush ? `${styles.panel} ${styles.flush}` : styles.panel}>
        {children}
      </BaseCollapsible.Panel>
    </BaseCollapsible.Root>
  );
}
