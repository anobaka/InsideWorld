/**
 * Reading the timestamps the server sends.
 *
 * The server serializes with Newtonsoft under
 * `DateFormatString = "yyyy-MM-dd HH:mm:ss.fff"` — a space where ISO 8601 puts a `T`,
 * and no offset at all, even though the value is `DateTime.UtcNow`. `new Date()` reads
 * a string in that shape as *local* time, so east of Greenwich every server timestamp
 * lands hours in the past. That is not a cosmetic drift: a freshly issued pairing code
 * was born already expired and vanished from the page, and "expires in N minutes" read
 * 0 forever.
 *
 * The desktop client hit the same format from the other side — its JSON reader rejected
 * it outright. Both now go through one deliberate answer to "what does a server
 * timestamp mean", rather than each guessing.
 */

/** `2026-09-12 09:22:29.278` or `2026-09-12T09:22:29`, with nothing saying which zone. */
const NAKED = /^\d{4}-\d{2}-\d{2}[ T]\d{2}:\d{2}:\d{2}(\.\d+)?$/;

/**
 * The instant a server timestamp refers to, or null when it is not one.
 *
 * A value that already carries a zone (`Z`, `+08:00`) is taken at its word; only the
 * naked form is assumed UTC, which is what the server means by it.
 */
export const parseServerTime = (value?: string | null): Date | null => {
  const raw = value?.trim();

  if (!raw) {
    return null;
  }

  const normalized = NAKED.test(raw) ? `${raw.replace(" ", "T")}Z` : raw;
  const parsed = new Date(normalized);

  return Number.isNaN(parsed.getTime()) ? null : parsed;
};

/**
 * Milliseconds from `now` until a server timestamp, never negative.
 *
 * Returns 0 for an unreadable value, which reads as "already over" — the safe direction
 * for a deadline: a caller shows an expired state rather than offering something that
 * no longer works.
 */
export const millisecondsUntil = (value?: string | null, now: number = Date.now()): number => {
  const at = parseServerTime(value);

  return at == null ? 0 : Math.max(0, at.getTime() - now);
};

/** Whole minutes until a server timestamp, rounded up so a live deadline never reads 0. */
export const minutesUntil = (value?: string | null, now: number = Date.now()): number =>
  Math.ceil(millisecondsUntil(value, now) / 60000);
