import { Checkbox as BaseCheckbox } from "@base-ui/react/checkbox";
import type { ReactNode } from "react";
import styles from "./Checkbox.module.css";
import { Icon } from "./Icon";

type Props = {
  checked: boolean;
  onChange: (checked: boolean) => void;
  children: ReactNode;
  // Muted text after the label, e.g. how many rows it matches.
  hint?: string;
  // Neither ticked nor unticked, e.g. a "pick all" box when only some are picked.
  indeterminate?: boolean;
};

export function Checkbox({ checked, onChange, children, hint, indeterminate = false }: Props) {
  return (
    <label className={styles.label}>
      <BaseCheckbox.Root
        className={styles.box}
        checked={checked}
        indeterminate={indeterminate}
        onCheckedChange={onChange}
      >
        <BaseCheckbox.Indicator className={styles.indicator}>
          <Icon name={indeterminate ? "minus" : "check"} size={14} />
        </BaseCheckbox.Indicator>
      </BaseCheckbox.Root>
      <span>{children}</span>
      {hint && <span className={styles.hint}>{hint}</span>}
    </label>
  );
}
