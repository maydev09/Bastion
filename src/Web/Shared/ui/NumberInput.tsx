import { NumberField } from "@base-ui/react/number-field";
import { useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import styles from "./NumberInput.module.css";

type Props = {
  value: number | null;
  // Called after a short pause in typing, on Enter and on leaving the field; null when it is cleared.
  onChange: (value: number | null) => void;
  label: string;
  placeholder?: string;
  min?: number;
  // The smallest step the field accepts: 1 for whole numbers.
  step?: number | "any";
};

const typingPause = 600;

// A number in the user's language ("3 500", "45,5"), typed freely and passed on once the user pauses.
export function NumberInput({ value, onChange, label, placeholder, min = 0, step = "any" }: Props) {
  const { i18n } = useTranslation();
  const [draft, setDraft] = useState(value);
  const [shown, setShown] = useState(value);
  const timer = useRef<ReturnType<typeof setTimeout>>(undefined);

  // A value changed from outside (e.g. "clear filters") replaces what the field shows.
  if (value !== shown) {
    setShown(value);
    setDraft(value);
  }

  useEffect(() => () => clearTimeout(timer.current), []);

  const commit = (next: number | null) => {
    clearTimeout(timer.current);
    if (next !== value) {
      onChange(next);
    }
  };

  return (
    <NumberField.Root
      className={styles.root}
      value={draft}
      min={min}
      step={step}
      locale={i18n.resolvedLanguage}
      onValueChange={(next) => {
        setDraft(next);
        clearTimeout(timer.current);
        timer.current = setTimeout(() => commit(next), typingPause);
      }}
      onValueCommitted={commit}
    >
      <NumberField.Input
        className={styles.input}
        aria-label={label}
        placeholder={placeholder}
        onKeyDown={(event) => {
          if (event.key === "Enter") {
            commit(draft);
          }
        }}
      />
    </NumberField.Root>
  );
}
