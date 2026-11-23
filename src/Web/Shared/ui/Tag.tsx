import type { ReactNode } from "react";
import styles from "./Tag.module.css";
import { Icon } from "./Icon";

type Props = {
  children: ReactNode;
  // Given with removeLabel, the tag gets a button that removes it, e.g. one filter in use.
  onRemove?: () => void;
  removeLabel?: string;
};

// A short label in a pill, e.g. one amenity of an offer.
export function Tag({ children, onRemove, removeLabel }: Props) {
  return (
    <span className={styles.tag}>
      {children}
      {onRemove && (
        <button type="button" className={styles.remove} aria-label={removeLabel} onClick={onRemove}>
          <Icon name="close" size={14} />
        </button>
      )}
    </span>
  );
}
