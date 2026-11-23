import { useTranslation } from "react-i18next";
import { Dialog } from "@shared/ui/Dialog";
import styles from "./ShortcutsHelp.module.css";

type Props = {
  open: boolean;
  onOpenChange: (open: boolean) => void;
};

// The keys are what the user presses; their names are the same in every language.
const groups = [
  {
    title: "shortcuts.list",
    keys: [
      { keys: ["↑", "↓"], action: "shortcuts.move" },
      { keys: ["Enter"], action: "shortcuts.open" },
      { keys: ["o"], action: "shortcuts.portal" },
      { keys: ["←", "→"], action: "shortcuts.page" },
      { keys: ["/"], action: "shortcuts.search" },
      { keys: ["f"], action: "shortcuts.filters" },
    ],
  },
  {
    title: "shortcuts.card",
    keys: [
      { keys: ["←", "→"], action: "shortcuts.neighbour" },
      { keys: ["Esc"], action: "shortcuts.back" },
      { keys: ["o"], action: "shortcuts.portal" },
    ],
  },
  {
    title: "shortcuts.everywhere",
    keys: [{ keys: ["?"], action: "shortcuts.help" }],
  },
] as const;

export function ShortcutsHelp({ open, onOpenChange }: Props) {
  const { t } = useTranslation("housing");
  return (
    <Dialog title={t("shortcuts.title")} open={open} onOpenChange={onOpenChange}>
      {groups.map((group) => (
        <section key={group.title} className={styles.group}>
          <h3>{t(group.title)}</h3>
          <dl>
            {group.keys.map((shortcut) => (
              <div key={shortcut.action + shortcut.keys.join()} className={styles.row}>
                <dt>
                  {shortcut.keys.map((key) => (
                    <kbd key={key}>{key}</kbd>
                  ))}
                </dt>
                <dd>{t(shortcut.action)}</dd>
              </div>
            ))}
          </dl>
        </section>
      ))}
    </Dialog>
  );
}
