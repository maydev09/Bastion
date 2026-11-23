// When the user last looked at the offers, so the list can show what is new since then.
// The previous visit is read once per browser tab (reloads keep it); while the list is shown, the last
// visit moves to now, so the next tab compares with when the user last saw the list, not when they opened it.
// Kept in this browser only; storage may be unavailable (private mode), and then there is no previous visit.
const lastVisitKey = "housing.lastVisit";
const previousVisitKey = "housing.previousVisit";

export function readPreviousVisit(): string | null {
  try {
    const kept = sessionStorage.getItem(previousVisitKey);
    if (kept !== null) {
      return kept || null;
    }
    const previous = localStorage.getItem(lastVisitKey);
    sessionStorage.setItem(previousVisitKey, previous ?? "");
    return previous;
  } catch {
    return null;
  }
}

export function rememberVisit() {
  try {
    localStorage.setItem(lastVisitKey, new Date().toISOString());
  } catch {
    // Without storage there is no last visit next time.
  }
}
