import type { ButtonHTMLAttributes } from "react";
import styles from "./Button.module.css";

type Props = ButtonHTMLAttributes<HTMLButtonElement> & {
  variant?: "primary" | "success";
};

export function Button({ variant = "primary", className, ...props }: Props) {
  return <button className={[styles.button, styles[variant], className].join(" ")} {...props} />;
}
