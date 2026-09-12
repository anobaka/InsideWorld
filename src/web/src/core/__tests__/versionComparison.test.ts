import { describe, expect, it } from "vitest";

import { compareAppVersions } from "../versionComparison";

/**
 * Which side the version notice tells somebody to update.
 *
 * Getting the direction wrong is worse than showing nothing: it sends a user to update
 * the machine that was already current, and leaves the one that was not.
 */
describe("comparing the client's version with the server's", () => {
  it("says nothing when they are the same build", () => {
    expect(compareAppVersions("2.4.0", "2.4.0")).toBe("same");
    expect(compareAppVersions("2.4.0-beta.5", "2.4.0-beta.5")).toBe("same");

    // Padding, a leading v, and build metadata are all the same version.
    expect(compareAppVersions("2.4", "2.4.0")).toBe("same");
    expect(compareAppVersions("v2.4.0", "2.4.0")).toBe("same");
    expect(compareAppVersions("2.4.0+abc123", "2.4.0+def456")).toBe("same");
  });

  it("orders by the numeric parts first", () => {
    expect(compareAppVersions("2.3.9", "2.4.0")).toBe("clientBehind");
    expect(compareAppVersions("2.4.0", "2.3.9")).toBe("serverBehind");
    expect(compareAppVersions("2.4.0", "10.0.0")).toBe("clientBehind");

    // Numerically, not as text: 10 is newer than 9, which a string compare gets backwards.
    expect(compareAppVersions("2.10.0", "2.9.0")).toBe("serverBehind");
  });

  it("treats a release as newer than any pre-release of it", () => {
    // The case this project hits on every stable release: a beta client against a
    // server that has moved to the release build of the same version.
    expect(compareAppVersions("2.4.0-beta.5", "2.4.0")).toBe("clientBehind");
    expect(compareAppVersions("2.4.0", "2.4.0-beta.5")).toBe("serverBehind");
  });

  it("orders one beta against another", () => {
    // Betas are where the two halves actually drift: a user-side endpoint added in
    // beta.7 is missing from a client still on beta.3, and this is what says so.
    expect(compareAppVersions("2.4.0-beta.3", "2.4.0-beta.7")).toBe("clientBehind");
    expect(compareAppVersions("2.4.0-beta.10", "2.4.0-beta.9")).toBe("serverBehind");
    expect(compareAppVersions("2.4.0-alpha.1", "2.4.0-beta.1")).toBe("clientBehind");

    // A numeric identifier ranks below a textual one, per semver.
    expect(compareAppVersions("2.4.0-1", "2.4.0-beta")).toBe("clientBehind");
  });

  it("refuses to order what it cannot read", () => {
    // Rather than guess. Somebody running a locally built copy should be told the two
    // differ, not sent to update whichever side the parser happened to rank lower.
    expect(compareAppVersions("dev", "2.4.0")).toBe("differ");
    expect(compareAppVersions("2.4.0", "")).toBe("differ");
    expect(compareAppVersions(undefined, "2.4.0")).toBe("differ");

    // Two unreadable versions that are nonetheless the same string are the same build.
    expect(compareAppVersions("dev", "dev")).toBe("same");
    expect(compareAppVersions(undefined, undefined)).toBe("same");
  });
});
