import { Collapsible as BaseCollapsible } from "@base-ui/react/collapsible";
import { useId, type ReactNode } from "react";
import styles from "./Collapsible.module.css";
import { Icon } from "./Icon";

type Props = {
  title: string;
  // A short line next to the title, visible while the panel is closed.
  hint?: string;
  // A number in a badge after the title when above 0, e.g. how many filters of the section are on.
  count?: number;
  // Shown on hover, e.g. "2 filters in use".
  countLabel?: string;
  defaultOpen?: boolean;
  // Given together when the caller keeps the state (e.g. remembers it).
  open?: boolean;
  onOpenChange?: (open: boolean) => void;
  // Content that sits at the edges, such as a table.
  flush?: boolean;
  // Without its own frame: one section of a panel, set apart from the next by a line.
  plain?: boolean;
  children: ReactNode;
};

export function Collapsible({
  title,
  hint,
  count = 0,
  countLabel,
  defaultOpen = false,
  open,
  onOpenChange,
  flush = false,
  plain = false,
  children,
}: Props) {
  const id = useId();
  const countId = count > 0 ? `${id}-count` : undefined;
  const hintId = hint ? `${id}-hint` : undefined;
  return (
    <BaseCollapsible.Root
      className={plain ? styles.plain : styles.root}
      defaultOpen={defaultOpen}
      open={open}
      onOpenChange={onOpenChange}
    >
      <BaseCollapsible.Trigger
        className={styles.trigger}
        aria-label={title}
        aria-describedby={[countId, hintId].filter(Boolean).join(" ") || undefined}
      >
        <span className={styles.chevron}>
          <Icon name="next" size={16} />
        </span>
        <span className={styles.title}>{title}</span>
        {count > 0 && (
          <span id={countId} className={styles.count} title={countLabel}>
            {count}
          </span>
        )}
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
