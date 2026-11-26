import { useState } from "react";
import { useTranslation } from "react-i18next";
import styles from "./DateInput.module.css";
import { Icon } from "./Icon";
import { IconButton } from "./IconButton";

type Props = {
  // A calendar day, "2026-11-25"; null when the field is empty.
  value: string | null;
  onChange: (value: string | null) => void;
  // Shown inside the field before the date ("Od", "Do") and read out as its name.
  label: string;
  min?: string;
  max?: string;
};

// A calendar day picked in the browser's own calendar, shown in the format of the app's language. A click anywhere in
// the field opens the calendar; a button clears it.
export function DateInput({ value, onChange, label, min, max }: Props) {
  const { t, i18n } = useTranslation();
  const [draft, setDraft] = useState(value ?? "");
  const [shown, setShown] = useState(value);

  // A value changed from outside (e.g. "clear filters") replaces what the field shows.
  if (value !== shown) {
    setShown(value);
    setDraft(value ?? "");
  }

  return (
    <span className={styles.field}>
      <label className={styles.picker}>
        <Icon name="calendar" size={16} />
        <span className={styles.label}>{label}</span>
        <span className={styles.value}>
          {value ? formatDay(value, i18n.resolvedLanguage ?? "pl") : t("date.any")}
        </span>
        {/* The browser's own field writes the date in the browser's language, whatever the app's language, so it
            stays invisible over the whole field: it still opens the calendar and takes a typed date. */}
        <input
          type="date"
          className={styles.input}
          value={draft}
          min={min}
          max={max}
          onChange={(event) => {
            const next = event.target.value;
            setDraft(next);
            // While a year is typed digit by digit the field already holds 0002, 0020, 0202: only a whole year counts.
            if (next === "" || Number(next.slice(0, 4)) >= 1000) {
              onChange(next || null);
            }
          }}
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
      {value && (
        <IconButton
          icon="close"
          label={t("date.clear")}
          className={styles.clear}
          onClick={() => onChange(null)}
        />
      )}
    </span>
  );
}

function formatDay(day: string, language: string) {
  return new Intl.DateTimeFormat(language, { dateStyle: "medium" }).format(
    new Date(`${day}T00:00`),
  );
}
