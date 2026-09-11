"use client";

import type { BakabaseServiceModelsViewChangelogRangeViewModel } from "@/sdk/Api";

import React, { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";

import { ChangelogBrowser } from "./ChangelogBrowser";
import { ChangelogViewer, RELEASES_URL } from "./ChangelogViewer";

import { Chip, Spinner } from "@/components/bakaui";
import ExternalLink from "@/components/ExternalLink";
import BApi from "@/sdk/BApi";

interface ChangelogRangeViewProps {
  /** The version the reader has now, exclusive. */
  from: string;
  /** The version they are moving to, inclusive. */
  to: string;
  fallbackUrl?: string;
}

/** Keeps the modal from resizing under the reader between states. */
const SHELL = "flex flex-col gap-3 h-[60vh] min-h-0";

/**
 * Everything one update changes — every release between the version the reader has and
 * the one they are about to install.
 *
 * The span is resolved by the server, which owns a real SemVer implementation; this
 * component never orders or filters versions itself. A span that cannot be resolved
 * collapses to the target's own notes rather than widening to the whole archive, and
 * says which of those two things happened.
 */
export const ChangelogRangeView = ({ from, to, fallbackUrl }: ChangelogRangeViewProps) => {
  const { t } = useTranslation();
  const [range, setRange] = useState<
    BakabaseServiceModelsViewChangelogRangeViewModel | null | undefined
  >();
  const [selected, setSelected] = useState<string>();

  useEffect(() => {
    let cancelled = false;

    BApi.changelog
      .getChangelogRange({ from, to })
      .then((rsp) => {
        if (cancelled) return;
        setRange(rsp.data ?? null);
        // The server echoes the bounds it resolved; the target row is guaranteed to be
        // first, so this selects the version being installed.
        setSelected(rsp.data?.releases?.[0]?.version ?? rsp.data?.to);
      })
      .catch(() => {
        if (!cancelled) setRange(null);
      });

    return () => {
      cancelled = true;
    };
  }, [from, to]);

  if (range === undefined) {
    return (
      <div className={`${SHELL} items-center justify-center`}>
        <Spinner size="sm" />
      </div>
    );
  }

  // Unresolvable span (bounds the server could not place, or an unreachable archive).
  // Distinct wording from the one-version case below: a reader behind a blocked CDN
  // should not conclude the update contains a single version's worth of changes.
  if (range === null) {
    return (
      <div className={SHELL}>
        <div className="text-xs text-foreground-500">
          {t<string>("changelog.range.unavailable")}{" "}
          <ExternalLink href={fallbackUrl ?? RELEASES_URL} size="sm">
            {t<string>("changelog.viewAllOnGithub")}
          </ExternalLink>
        </div>
        <div className="flex-1 min-h-0 overflow-y-auto">
          <ChangelogViewer fallbackUrl={fallbackUrl} version={to} />
        </div>
      </div>
    );
  }

  const releases = range.releases ?? [];
  const hidden = range.hiddenPrereleaseCount ?? 0;

  return (
    <div className={SHELL}>
      <div className="flex items-center gap-2 flex-wrap text-xs text-foreground-500">
        <Chip radius="sm" size="sm" variant="flat">
          {range.from} → {range.to}
        </Chip>
        <span>{t<string>("changelog.range.count", { count: releases.length })}</span>
        {/* A stable update legitimately collapses to one row — its notes are generated
            from the previous stable release and already cover the pre-releases between.
            Say so, or the filter reads as a button that did nothing. */}
        {hidden > 0 && (
          <span>· {t<string>("changelog.range.hiddenPreReleases", { count: hidden })}</span>
        )}
        <ExternalLink className="ml-auto" href={range.releasesUrl ?? RELEASES_URL} size="sm">
          {t<string>("changelog.viewAllOnGithub")}
        </ExternalLink>
      </div>

      {/* No current-version marker: everything listed here is new to this reader. */}
      <ChangelogBrowser
        hideRailWhenSingle
        className="flex-1"
        releases={releases}
        selected={selected}
        onSelect={setSelected}
      />
    </div>
  );
};

ChangelogRangeView.displayName = "ChangelogRangeView";
