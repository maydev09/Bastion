import styles from "./DateInput.module.css";
import { Icon } from "./Icon";

type Props = {
  // A calendar day, "2026-11-25"; null when the field is empty.
  value: string | null;
  onChange: (value: string | null) => void;
  // Shown inside the field before the date ("Od", "Do") and read out as its name.
  label: string;
  min?: string;
  max?: string;
};

// A calendar day in the browser's own date field and format. A click anywhere in the field opens the calendar.
export function DateInput({ value, onChange, label, min, max }: Props) {
  return (
    <label className={styles.field}>
      <Icon name="calendar" size={16} />
      <span className={styles.label}>{label}</span>
      <input
        type="date"
        className={styles.input}
        value={value ?? ""}
        min={min}
        max={max}
        onChange={(event) => onChange(event.target.value || null)}
        onClick={(event) => {
          // Not every browser can open its calendar from code; the field still takes a typed date.
          try {
            event.currentTarget.showPicker();
          } catch {
            // Nothing to do.
          }
        }}
      />
    </label>
  );
}
