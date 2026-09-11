/**
 * Comparing the client's version with the server's.
 *
 * The two are separate products with separate update channels, so they drift apart as a
 * matter of course. The protocol handshake already refuses a pair that cannot talk at
 * all; this covers the far more common case where they can talk, and one of them is
 * missing something the other knows about — a user-side endpoint the client has not
 * learned yet, which otherwise surfaces as "this needs a newer client" at the moment
 * somebody tries to use it.
 */

export type VersionRelation =
  | "same"
  /** The client is older; updating it is what closes the gap. */
  | "clientBehind"
  /** The server is older. */
  | "serverBehind"
  /** They differ, but not in a way that can be ordered. */
  | "differ";

interface Parsed {
  core: number[];
  prerelease: string[];
}

/**
 * Splits `2.4.0-beta.5` into its parts, or null for anything that is not a version.
 *
 * Build metadata (`+sha`) is dropped rather than compared: semver says it takes no part
 * in precedence, and two builds of the same version are the same program.
 */
const parse = (value: string | undefined): Parsed | null => {
  const trimmed = (value ?? "").trim().replace(/^v/i, "").split("+")[0];

  if (!trimmed) {
    return null;
  }

  const [core, ...rest] = trimmed.split("-");
  const numbers = core.split(".").map((p) => Number(p));

  if (numbers.length === 0 || numbers.some((n) => !Number.isInteger(n) || n < 0)) {
    return null;
  }

  // Missing parts read as zero, so "2.4" and "2.4.0" are the same version.
  while (numbers.length < 3) {
    numbers.push(0);
  }

  const prerelease = rest.join("-");

  return { core: numbers, prerelease: prerelease ? prerelease.split(".") : [] };
};

/** Semver identifier precedence: numbers before text, numbers numerically, text lexically. */
const compareIdentifiers = (a: string, b: string): number => {
  const numericA = /^\d+$/.test(a);
  const numericB = /^\d+$/.test(b);

  if (numericA && numericB) {
    return Number(a) - Number(b);
  }

  if (numericA !== numericB) {
    // A numeric identifier always has lower precedence than a non-numeric one.
    return numericA ? -1 : 1;
  }

  return a < b ? -1 : a > b ? 1 : 0;
};

const comparePrerelease = (a: string[], b: string[]): number => {
  // A release outranks any pre-release of the same core: 2.4.0 is newer than 2.4.0-beta.
  if (a.length === 0 || b.length === 0) {
    return a.length === b.length ? 0 : a.length === 0 ? 1 : -1;
  }

  for (let i = 0; i < Math.max(a.length, b.length); i++) {
    if (i >= a.length) return -1;
    if (i >= b.length) return 1;

    const result = compareIdentifiers(a[i], b[i]);

    if (result !== 0) {
      return result;
    }
  }

  return 0;
};

/**
 * Which of the two is behind, if either.
 *
 * A version neither side can parse answers `differ` rather than guessing an order: a
 * notice that names the wrong side to upgrade is worse than one that says only that
 * they are not the same.
 */
export const compareAppVersions = (
  clientVersion: string | undefined,
  serverVersion: string | undefined,
): VersionRelation => {
  const client = parse(clientVersion);
  const server = parse(serverVersion);

  if (!client || !server) {
    return (clientVersion ?? "").trim() === (serverVersion ?? "").trim() ? "same" : "differ";
  }

  for (let i = 0; i < Math.max(client.core.length, server.core.length); i++) {
    const difference = (client.core[i] ?? 0) - (server.core[i] ?? 0);

    if (difference !== 0) {
      return difference < 0 ? "clientBehind" : "serverBehind";
    }
  }

  const prerelease = comparePrerelease(client.prerelease, server.prerelease);

  return prerelease === 0 ? "same" : prerelease < 0 ? "clientBehind" : "serverBehind";
};
