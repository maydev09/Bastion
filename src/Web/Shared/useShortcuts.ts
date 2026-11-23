import { useEffect, useRef } from "react";

// What a key does on the current page, by KeyboardEvent.key ("ArrowDown", "Enter", "o", "?").
export type Shortcuts = Record<string, () => void>;

// Keyboard shortcuts of a page. They stay out of the way: nothing happens while the user types in a field,
// works in a dialog, or holds Ctrl, Alt or Cmd (the browser's own shortcuts), and Enter or Space on a link or a button keeps
// doing what it does there. enabled: false pauses them, e.g. while a dialog is open.
export function useShortcuts(shortcuts: Shortcuts, enabled = true) {
  // The latest handlers, so the listener is added once and still calls the current ones.
  const current = useRef(shortcuts);
  useEffect(() => {
    current.current = shortcuts;
  });

  useEffect(() => {
    if (!enabled) {
      return;
    }
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.defaultPrevented || event.ctrlKey || event.altKey || event.metaKey) {
        return;
      }
      const target = event.target instanceof Element ? event.target : null;
      // Typing in a field, or any key inside a dialog over the page, is not meant for the page.
      if (
        target?.closest(
          "input, textarea, select, [contenteditable='true'], [role='combobox'], [role='dialog']",
        )
      ) {
        return;
      }
      if ((event.key === "Enter" || event.key === " ") && target?.closest("a, button")) {
        return;
      }
      const action = current.current[event.key];
      if (action) {
        event.preventDefault();
        action();
      }
    };
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, [enabled]);
}
