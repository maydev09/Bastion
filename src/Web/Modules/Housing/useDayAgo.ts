import { useEffect, useState } from "react";

const day = 24 * 60 * 60 * 1000;
const refresh = 5 * 60 * 1000;

// The moment 24 hours ago, moved forward every few minutes, so a page left open still means the last day.
export function useDayAgo() {
  const [now, setNow] = useState(Date.now);
  useEffect(() => {
    const timer = setInterval(() => setNow(Date.now()), refresh);
    return () => clearInterval(timer);
  }, []);
  return new Date(now - day).toISOString();
}
