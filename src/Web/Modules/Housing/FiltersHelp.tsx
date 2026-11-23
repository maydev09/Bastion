import { useTranslation } from "react-i18next";
import { Dialog } from "@shared/ui/Dialog";
import styles from "./FiltersHelp.module.css";

type Props = {
  open: boolean;
  onOpenChange: (open: boolean) => void;
};

const rules = ["sections", "choices", "features", "ranges", "history", "counts"] as const;

// How the filters join: AND between sections, OR inside one, and the exceptions.
export function FiltersHelp({ open, onOpenChange }: Props) {
  const { t } = useTranslation("housing");
  return (
    <Dialog title={t("filtersHelp.title")} open={open} onOpenChange={onOpenChange}>
      <ul className={styles.rules}>
        {rules.map((rule) => (
          <li key={rule}>{t(`filtersHelp.${rule}`)}</li>
        ))}
      </ul>
    </Dialog>
  );
}
