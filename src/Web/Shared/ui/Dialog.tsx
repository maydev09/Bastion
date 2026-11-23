import { Dialog as BaseDialog } from "@base-ui/react/dialog";
import type { ReactNode } from "react";
import styles from "./Dialog.module.css";
import { IconButton } from "./IconButton";

type Props = {
  title: string;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  // The accessible name of the close button, e.g. "Close".
  closeLabel: string;
  children: ReactNode;
};

// A window over the page, closed with its button, Esc or a click beside it.
export function Dialog({ title, open, onOpenChange, closeLabel, children }: Props) {
  return (
    <BaseDialog.Root open={open} onOpenChange={onOpenChange}>
      <BaseDialog.Portal>
        <BaseDialog.Backdrop className={styles.backdrop} />
        <BaseDialog.Popup className={styles.popup}>
          <div className={styles.header}>
            <BaseDialog.Title className={styles.title}>{title}</BaseDialog.Title>
            <BaseDialog.Close render={<IconButton icon="close" label={closeLabel} />} />
          </div>
          <div className={styles.body}>{children}</div>
        </BaseDialog.Popup>
      </BaseDialog.Portal>
    </BaseDialog.Root>
  );
}
