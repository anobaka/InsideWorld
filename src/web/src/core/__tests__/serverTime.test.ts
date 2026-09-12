import { describe, expect, it } from "vitest";

import { millisecondsUntil, minutesUntil, parseServerTime } from "../serverTime";

/**
 * Reading what the server actually sends.
 *
 * The bug these pin: `new Date("2026-09-12 09:22:29.278")` is read as *local* time, but
 * the server stamped it with `DateTime.UtcNow` and its serializer dropped the marker
 * saying so. East of Greenwich that put every deadline hours in the past — a pairing
 * code was cleared from the page the instant it was issued, and the minutes left read 0
 * for a code with a full ten minutes to run.
 */
describe("reading a server timestamp", () => {
  // Captured verbatim from 2.4.0-beta.242's /remote-access/server-info.
  const CAPTURED = "2026-09-12 09:22:29.278";
  const CAPTURED_UTC = Date.UTC(2026, 8, 12, 9, 22, 29, 278);

  it("takes a timestamp with no zone as UTC", () => {
    expect(parseServerTime(CAPTURED)?.getTime()).toBe(CAPTURED_UTC);

    // Whatever zone the test machine is in, the answer is the same instant. Written with
    // an explicit offset so the assertion cannot pass by accident on a UTC runner while
    // failing on the developer's laptop.
    expect(parseServerTime(CAPTURED)?.toISOString()).toBe("2026-09-12T09:22:29.278Z");

    // Seconds-only and a T separator are the same shape, still zoneless.
    expect(parseServerTime("2026-09-12T09:22:29")?.toISOString()).toBe("2026-09-12T09:22:29.000Z");
  });

  it("takes a timestamp that states its zone at its word", () => {
    expect(parseServerTime("2026-09-12T09:22:29.278Z")?.getTime()).toBe(CAPTURED_UTC);
    expect(parseServerTime("2026-09-12T17:22:29.278+08:00")?.getTime()).toBe(CAPTURED_UTC);
  });

  it("answers null rather than an Invalid Date", () => {
    expect(parseServerTime(undefined)).toBeNull();
    expect(parseServerTime(null)).toBeNull();
    expect(parseServerTime("")).toBeNull();
    expect(parseServerTime("   ")).toBeNull();
    expect(parseServerTime("not a time")).toBeNull();
  });

  it("counts down to a deadline instead of past it", () => {
    const now = CAPTURED_UTC;

    // Ten minutes out reads as ten minutes, not as zero — which is what the page showed
    // for every live pairing code before this.
    expect(minutesUntil("2026-09-12 09:32:29.278", now)).toBe(10);
    expect(millisecondsUntil("2026-09-12 09:32:29.278", now)).toBe(600000);

    // Rounded up, so the last partial minute of a live code still reads 1.
    expect(minutesUntil("2026-09-12 09:22:59.278", now)).toBe(1);
  });

  it("treats what it cannot read, and what has passed, as over", () => {
    const now = CAPTURED_UTC;

    expect(millisecondsUntil("2026-09-12 09:12:29.278", now)).toBe(0);
    expect(minutesUntil("2026-09-12 09:12:29.278", now)).toBe(0);

    // Unreadable answers "over" rather than "forever": a caller shows an expired state
    // instead of offering something that no longer works.
    expect(millisecondsUntil("not a time", now)).toBe(0);
    expect(millisecondsUntil(undefined, now)).toBe(0);
  });
});
