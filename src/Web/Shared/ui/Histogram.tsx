import styles from "./Histogram.module.css";
import { VisuallyHidden } from "./VisuallyHidden";

type Props = {
  // What the chart shows, e.g. "Prices of the offers from 1 000 to 9 000 zł".
  label: string;
  counts: number[];
  // Whether a bar lies in the chosen range; without a range every bar is picked.
  isPicked: (index: number) => boolean;
  // The range and count of a bar, shown on hover.
  barLabel: (index: number) => string;
  // A click on a bar, e.g. to narrow a filter to its range.
  onPick: (index: number) => void;
  // The values at the left and the right edge.
  start: string;
  end: string;
};

// A small bar chart of how many items fall into each range of a value, e.g. above a price filter.
// The bars are a mouse shortcut: they stay out of the tab order and screen readers, which use the
// fields of the filter instead.
export function Histogram({ label, counts, isPicked, barLabel, onPick, start, end }: Props) {
  const max = Math.max(...counts, 1);
  return (
    <figure className={styles.histogram}>
      <VisuallyHidden>{label}</VisuallyHidden>
      <div className={styles.bars} aria-hidden="true">
        {counts.map((count, index) => (
          <button
            // The bars are fixed slots of one chart; their order never changes.
            key={index}
            type="button"
            tabIndex={-1}
            className={styles.bar}
            data-picked={isPicked(index) || undefined}
            title={barLabel(index)}
            onClick={() => onPick(index)}
          >
            {/* A bar with few items stays visible as a sliver. */}
            <span style={{ height: count > 0 ? `${Math.max((count / max) * 100, 4)}%` : 0 }} />
          </button>
        ))}
      </div>
      <div className={styles.axis} aria-hidden="true">
        <span>{start}</span>
        <span>{end}</span>
      </div>
    </figure>
  );
}
