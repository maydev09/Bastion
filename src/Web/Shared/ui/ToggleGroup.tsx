import { Toggle } from "@base-ui/react/toggle";
import styles from "./ToggleGroup.module.css";

type Props<Value extends string> = {
  // The accessible name of the group.
  label: string;
  // A disabled item shows greyed out, e.g. a choice that makes no sense yet.
  items: { value: Value; label: string; disabled?: boolean }[];
  // The values whose buttons are pressed.
  pressed: Value[];
  onToggle: (value: Value) => void;
  // One under the other, e.g. in a narrow panel; side by side otherwise.
  vertical?: boolean;
};

// Joined buttons that stay pressed, e.g. a choice of one mode or a few numbers next to each other.
// What a click does is the caller's: it may press one button only, or several.
export function ToggleGroup<Value extends string>({
  label,
  items,
  pressed,
  onToggle,
  vertical = false,
}: Props<Value>) {
  return (
    <div
      role="group"
      aria-label={label}
      className={vertical ? `${styles.group} ${styles.vertical}` : styles.group}
    >
      {items.map((item) => (
        <Toggle
          key={item.value}
          className={styles.toggle}
          pressed={pressed.includes(item.value)}
          disabled={item.disabled}
          onPressedChange={() => onToggle(item.value)}
        >
          {item.label}
        </Toggle>
      ))}
    </div>
  );
}
