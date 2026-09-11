"use client";

import React from "react";
import { useTranslation } from "react-i18next";
import { AiOutlineFileText } from "react-icons/ai";

import { ChangelogRangeView } from "./ChangelogRangeView";
import { ChangelogViewer } from "./ChangelogViewer";

import { Button, Modal, Tooltip } from "@/components/bakaui";
import { useBakabaseContext } from "@/components/ContextProvider/BakabaseContextProvider";

export { ChangelogBrowser } from "./ChangelogBrowser";
export { ChangelogMarkdown, ChangelogViewer, RELEASES_URL } from "./ChangelogViewer";
export { ChangelogRangeView } from "./ChangelogRangeView";

export interface ChangelogModalOptions {
  /**
   * The version the reader has now. When given, the modal shows every release from it up
   * to the target instead of the target alone — what one update actually contains.
   * Omit where there is no meaningful lower bound (the button beside the running version).
   */
  from?: string;
  fallbackUrl?: string;
}

/** Opens a version's notes — or a whole update's worth of notes — in a modal. */
export const useChangelogModal = () => {
  const { t } = useTranslation();
  const { createPortal } = useBakabaseContext();

  return (version?: string, options?: ChangelogModalOptions) => {
    const { from, fallbackUrl } = options ?? {};
    // A span is only meaningful between two different versions; anything else is a
    // single release. The server still re-checks, and collapses on its own terms.
    const isRange = Boolean(from && version && from !== version);

    createPortal(Modal, {
      size: "xl",
      // A range title is NOT built from these arguments: the server may resolve
      // different bounds than were asked for, so the resolved span is stated inside the
      // body instead, where it cannot disagree with what is on screen.
      title: isRange
        ? t<string>("changelog.title")
        : version
          ? t<string>("changelog.titleWithVersion", { version })
          : t<string>("changelog.title"),
      defaultVisible: true,
      // The modal body is itself a scroller (scrollBehavior="inside"); the range view
      // brings its own panes, so let it own the scrolling instead of nesting two.
      classNames: isRange ? { body: "overflow-hidden" } : undefined,
      children: isRange ? (
        <ChangelogRangeView fallbackUrl={fallbackUrl} from={from!} to={version!} />
      ) : (
        <ChangelogViewer fallbackUrl={fallbackUrl} version={version} />
      ),
      footer: { actions: ["cancel"] },
    });
  };
};

interface ChangelogButtonProps {
  version?: string;
  /** See ChangelogModalOptions.from. */
  from?: string;
  /** Icon-only, for the sidebar where a label does not fit. */
  isIconOnly?: boolean;
  size?: "sm" | "md";
  className?: string;
}

/**
 * The "what changed" affordance that sits next to every update action, so a user is
 * never asked to install a version they cannot read about first.
 */
export const ChangelogButton = ({
  version,
  from,
  isIconOnly,
  size = "sm",
  className,
}: ChangelogButtonProps) => {
  const { t } = useTranslation();
  const showChangelog = useChangelogModal();
  const label = t<string>("changelog.view");

  const button = (
    <Button
      aria-label={label}
      className={className}
      color="secondary"
      isIconOnly={isIconOnly}
      size={size}
      variant="light"
      onPress={() => showChangelog(version, { from })}
    >
      <AiOutlineFileText className="text-base" />
      {!isIconOnly && label}
    </Button>
  );

  return isIconOnly ? (
    <Tooltip content={label} placement="right">
      {button}
    </Tooltip>
  ) : (
    button
  );
};
