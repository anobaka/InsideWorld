"use client";

import type { BakabaseServiceModelsViewChangelogReleaseViewModel } from "@/sdk/Api";

import React from "react";
import { useTranslation } from "react-i18next";

import { ChangelogViewer } from "./ChangelogViewer";

import { Chip } from "@/components/bakaui";

interface ChangelogBrowserProps {
  /** Already filtered and ordered by the caller — this component decides nothing. */
  releases: BakabaseServiceModelsViewChangelogReleaseViewModel[];
  selected?: string;
  onSelect: (version: string) => void;
  /** Marked in the rail so a reader can see where they are. */
  currentVersion?: string;
  /**
   * Drop the rail when there is only one release to read. The history page keeps its
   * rail either way — a one-row list still reads as a list there — while a modal
   * opened on a single version should just show the notes.
   */
  hideRailWhenSingle?: boolean;
  className?: string;
}

/**
 * The version rail beside one release's notes. Shared by the /changelog page and the
 * update modal so the two cannot drift apart; both own their own filtering and
 * selection state and hand this component the result.
 */
export const ChangelogBrowser = ({
  releases,
  selected,
  onSelect,
  currentVersion,
  hideRailWhenSingle,
  className,
}: ChangelogBrowserProps) => {
  const { t } = useTranslation();

  const showRail = !(hideRailWhenSingle && releases.length <= 1);

  return (
    <div className={`flex gap-3 min-h-0 ${className ?? "flex-1"}`}>
      {showRail && (
        <div className="w-[240px] shrink-0 overflow-y-auto rounded-md border border-default-200">
          {releases.map((r) => {
            const isSelected = r.version === selected;

            return (
              <button
                key={r.version}
                className={`flex w-full flex-col items-start gap-1 border-b border-default-100 px-3 py-2 text-left last:border-b-0 ${
                  isSelected ? "bg-default-100" : "hover:bg-default-50"
                }`}
                type="button"
                onClick={() => onSelect(r.version)}
              >
                <div className="flex items-center gap-1 flex-wrap">
                  <span className="text-sm font-medium break-all">{r.version}</span>
                  {r.version === currentVersion && (
                    <Chip color="success" radius="sm" size="sm" variant="flat">
                      {t<string>("changelog.current")}
                    </Chip>
                  )}
                  {r.prerelease && (
                    <Chip color="warning" radius="sm" size="sm" variant="flat">
                      {t<string>("changelog.preRelease")}
                    </Chip>
                  )}
                </div>
                {r.publishedAt && (
                  <span className="text-xs text-foreground-400">
                    {new Date(r.publishedAt).toLocaleDateString()}
                  </span>
                )}
              </button>
            );
          })}
          {releases.length === 0 && (
            <div className="p-3 text-sm text-foreground-500">{t<string>("changelog.empty")}</div>
          )}
        </div>
      )}

      <div className="flex-1 min-w-0 overflow-y-auto rounded-md border border-default-200 p-4">
        <ChangelogViewer version={selected} />
      </div>
    </div>
  );
};

ChangelogBrowser.displayName = "ChangelogBrowser";
