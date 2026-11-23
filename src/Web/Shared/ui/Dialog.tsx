import { Dialog as BaseDialog } from "@base-ui/react/dialog";
import type { ReactNode } from "react";
import { useTranslation } from "react-i18next";
import styles from "./Dialog.module.css";
import { IconButton } from "./IconButton";

type Props = {
  title: string;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  children: ReactNode;
};

// A window over the page, closed with its button, Esc or a click beside it.
export function Dialog({ title, open, onOpenChange, children }: Props) {
  const { t } = useTranslation();
  return (
    <BaseDialog.Root open={open} onOpenChange={onOpenChange}>
      <BaseDialog.Portal>
        <BaseDialog.Backdrop className={styles.backdrop} />
        <BaseDialog.Popup className={styles.popup}>
          <div className={styles.header}>
            <BaseDialog.Title className={styles.title}>{title}</BaseDialog.Title>
            <BaseDialog.Close render={<IconButton icon="close" label={t("dialog.close")} />} />
          </div>
          <div className={styles.body}>{children}</div>
        </BaseDialog.Popup>
      </BaseDialog.Portal>
    </BaseDialog.Root>
  );
}
