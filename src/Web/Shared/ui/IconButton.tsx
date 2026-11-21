import type { ButtonHTMLAttributes } from "react";
import styles from "./IconButton.module.css";
import { Icon, type IconName } from "./Icon";

type Props = Omit<ButtonHTMLAttributes<HTMLButtonElement>, "children"> & {
  icon: IconName;
  // The accessible name, also shown as a tooltip.
  label: string;
};

export function IconButton({ icon, label, type = "button", className, ...props }: Props) {
  return (
    <button
      type={type}
      aria-label={label}
      title={label}
      className={[styles.button, className].join(" ")}
      {...props}
    >
      <Icon name={icon} />
    </button>
  );
}
