"use client";

import type { BakabaseServiceModelsViewChangelogIndexViewModel } from "@/sdk/Api";

import React, { useEffect, useMemo, useState } from "react";
import { useTranslation } from "react-i18next";

import { Spinner, Switch } from "@/components/bakaui";
import { ChangelogBrowser, RELEASES_URL } from "@/components/Changelog";
import ExternalLink from "@/components/ExternalLink";
import BApi from "@/sdk/BApi";

/**
 * The release history, read from the archive CI mirrors onto the update CDN
 * (never the GitHub API from the user's machine — see ChangelogService).
 */
const ChangelogPage = () => {
  const { t } = useTranslation();

  const [index, setIndex] = useState<
    BakabaseServiceModelsViewChangelogIndexViewModel | null | undefined
  >();
  const [currentVersion, setCurrentVersion] = useState<string>();
  const [selected, setSelected] = useState<string>();
  // Pre-releases outnumber stable releases by more than an order of magnitude,
  // so the stable line would be unfindable if they were mixed in by default.
  const [showPreReleases, setShowPreReleases] = useState(false);

  useEffect(() => {
    BApi.changelog
      .getChangelogReleases()
      .then((rsp) => setIndex(rsp.data ?? null))
      .catch(() => setIndex(null));
    BApi.app
      .getAppInfo()
      .then((rsp) => setCurrentVersion(rsp.data?.coreVersion ?? undefined))
      .catch(() => {});
  }, []);

  const releases = useMemo(() => {
    const all = index?.releases ?? [];

    // The running build stays listed whatever the filter says: hiding the
    // version the user is on is the one thing this page must never do.
    return all.filter((r) => showPreReleases || !r.prerelease || r.version === currentVersion);
  }, [index, showPreReleases, currentVersion]);

  useEffect(() => {
    if (releases.length === 0) {
      setSelected(undefined);

      return;
    }
    if (!selected || !releases.some((r) => r.version === selected)) {
      setSelected(releases[0].version);
    }
  }, [releases, selected]);

  if (index === undefined) {
    return (
      <div className="flex items-center justify-center py-16">
        <Spinner size="sm" />
      </div>
    );
  }

  if (index === null) {
    return (
      <div className="flex flex-col items-center gap-2 py-16 text-sm text-foreground-500">
        <span>{t<string>("changelog.indexUnavailable")}</span>
        <ExternalLink href={RELEASES_URL}>{t<string>("changelog.viewOnGithub")}</ExternalLink>
      </div>
    );
  }

  return (
    <div className="flex flex-col gap-3 h-full min-h-0 p-2">
      <div className="flex items-center justify-between gap-3 flex-wrap">
        <div className="text-lg font-semibold">{t<string>("changelog.title")}</div>
        <div className="flex items-center gap-4">
          <label className="flex items-center gap-2 text-sm text-foreground-500">
            {t<string>("changelog.showPreReleases")}
            <Switch isSelected={showPreReleases} size="sm" onValueChange={setShowPreReleases} />
          </label>
          <ExternalLink href={index.releasesUrl ?? RELEASES_URL} size="sm">
            {t<string>("changelog.viewAllOnGithub")}
          </ExternalLink>
        </div>
      </div>

      {/* The rail and notes pane are shared with the update modal so the two surfaces
          cannot drift; filtering and selection stay here. */}
      <ChangelogBrowser
        currentVersion={currentVersion}
        releases={releases}
        selected={selected}
        onSelect={setSelected}
      />
    </div>
  );
};

ChangelogPage.displayName = "ChangelogPage";

export default ChangelogPage;
