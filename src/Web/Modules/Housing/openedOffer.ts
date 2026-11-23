// The offer the user opened last stays marked when they come back to the list.
// Kept for the browser tab, so the mark also survives a reload; storage may be unavailable (private mode).
const openedOfferKey = "housing.openedOffer";

export function readOpenedOffer(): string | null {
  try {
    return sessionStorage.getItem(openedOfferKey);
  } catch {
    return null;
  }
}

export function rememberOpenedOffer(id: number) {
  try {
    sessionStorage.setItem(openedOfferKey, String(id));
  } catch {
    // Without storage the list simply marks nothing.
  }
}
