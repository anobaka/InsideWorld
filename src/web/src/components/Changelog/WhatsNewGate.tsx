"use client";

import { useEffect, useRef } from "react";

import { useChangelogModal } from "@/components/Changelog";
import BApi from "@/sdk/BApi";

const STORAGE_KEY = "bakabase.changelog.lastSeenVersion";

const readLastSeen = (): string | null => {
  try {
    return localStorage.getItem(STORAGE_KEY);
  } catch {
    // Private mode, or storage disabled — then this is simply never shown.
    return null;
  }
};

const writeLastSeen = (version: string) => {
  try {
    localStorage.setItem(STORAGE_KEY, version);
  } catch {
    // Nothing to do: at worst the notes are offered again next launch.
  }
};

/**
 * Shows the running version's release notes once, the first time the app runs
 * after an upgrade. A fresh install records the version silently instead — a
 * user who has never seen this app does not want a changelog as their first
 * screen. The marker is per-browser-profile rather than server state, so the
 * worst failure is showing (or skipping) the notes once.
 */
const WhatsNewGate = () => {
  const showChangelog = useChangelogModal();
  const handled = useRef(false);

  useEffect(() => {
    if (handled.current) return;
    handled.current = true;

    BApi.app
      .getAppInfo()
      .then(async (rsp) => {
        const version = rsp.data?.coreVersion;

        if (!version) return;

        const lastSeen = readLastSeen();

        // Recorded either way, so a version whose notes never arrive cannot
        // re-ask on every launch.
        writeLastSeen(version);

        if (!lastSeen || lastSeen === version) return;

        // Confirm there is something to read before opening a modal over the
        // app: a dev build, or a release older than the archive, has no notes.
        const changelog = await BApi.changelog.getChangelog({ version });

        if (changelog.data) {
          // lastSeen is this reader's true previous version, so the modal can show
          // every release they skipped — several, if they were behind more than one.
          showChangelog(version, { from: lastSeen });
        }
      })
      .catch(() => {});
  }, []);

  return null;
};

WhatsNewGate.displayName = "WhatsNewGate";

export default WhatsNewGate;
