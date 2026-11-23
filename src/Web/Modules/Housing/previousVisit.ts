// When the user last looked at the offers, so the list can show what is new since then.
// A visit is a browser tab: reloads in the same tab keep the same previous visit, a new tab starts a new one.
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
    localStorage.setItem(lastVisitKey, new Date().toISOString());
    return previous;
  } catch {
    return null;
  }
}
