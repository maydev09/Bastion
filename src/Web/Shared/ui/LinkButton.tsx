import { Link, type LinkProps } from "react-router";
import styles from "./Button.module.css";

type Props = LinkProps & {
  variant?: "primary" | "secondary" | "ghost";
};

// A link that looks like a Button: it can still be opened in a new tab.
export function LinkButton({ variant = "secondary", className, ...props }: Props) {
  return <Link className={[styles.button, styles[variant], className].join(" ")} {...props} />;
}
