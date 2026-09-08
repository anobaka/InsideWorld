"use client";

import type { BakabaseServiceModelsViewChangelogViewModel } from "@/sdk/Api";

import React, { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import Markdown from "react-markdown";
import remarkGfm from "remark-gfm";
import { AiOutlineFileText } from "react-icons/ai";

import { Button, Modal, Spinner, Tooltip } from "@/components/bakaui";
import ExternalLink from "@/components/ExternalLink";
import BApi from "@/sdk/BApi";
import { useBakabaseContext } from "@/components/ContextProvider/BakabaseContextProvider";

/** Kept in sync with ChangelogService.ReleasesUrl on the server. */
export const RELEASES_URL = "https://github.com/anobaka/Bakabase/releases";

// Tailwind's preflight strips heading/list styling, and the project carries no
// typography plugin, so release notes would otherwise render as one flat block.
const markdownClassName = [
  "text-sm leading-relaxed break-words",
  "[&_h1]:text-lg [&_h1]:font-semibold [&_h1]:mt-4 [&_h1]:mb-2",
  "[&_h2]:text-base [&_h2]:font-semibold [&_h2]:mt-4 [&_h2]:mb-2",
  "[&_h3]:text-sm [&_h3]:font-semibold [&_h3]:mt-4 [&_h3]:mb-2",
  "[&_h4]:text-sm [&_h4]:font-semibold [&_h4]:mt-3 [&_h4]:mb-1",
  "[&_p]:my-2",
  "[&_ul]:list-disc [&_ul]:pl-5 [&_ul]:my-2",
  "[&_ol]:list-decimal [&_ol]:pl-5 [&_ol]:my-2",
  "[&_li]:my-1",
  "[&_hr]:my-4 [&_hr]:border-default-200",
  "[&_blockquote]:border-l-2 [&_blockquote]:border-default-300 [&_blockquote]:pl-3 [&_blockquote]:text-foreground-500",
  "[&_code]:rounded [&_code]:bg-default-100 [&_code]:px-1 [&_code]:py-0.5 [&_code]:text-xs",
  "[&_table]:my-2 [&_table]:w-full [&_table]:text-left",
  "[&_th]:border [&_th]:border-default-200 [&_th]:px-2 [&_th]:py-1 [&_th]:font-semibold",
  "[&_td]:border [&_td]:border-default-200 [&_td]:px-2 [&_td]:py-1",
  "[&_img]:inline-block",
].join(" ");

/** Release notes exactly as GitHub renders them — GFM tables included. */
export const ChangelogMarkdown = ({ children }: { children: string }) => (
  <div className={markdownClassName}>
    <Markdown
      components={{ a: (props) => <ExternalLink {...(props as any)} /> }}
      remarkPlugins={[remarkGfm]}
    >
      {children}
    </Markdown>
  </div>
);

interface ChangelogViewerProps {
  version?: string;
  /** Shown as a footer link when the notes cannot be fetched, too. */
  fallbackUrl?: string;
}

/**
 * One release's notes, fetched on demand. The notes live on the update CDN
 * rather than the GitHub API — see ChangelogService for why — so they can be
 * missing (a local build, a release older than the archive, a blocked CDN);
 * every such case ends at the GitHub link instead of an error.
 */
export const ChangelogViewer = ({ version, fallbackUrl }: ChangelogViewerProps) => {
  const { t } = useTranslation();
  const [changelog, setChangelog] = useState<
    BakabaseServiceModelsViewChangelogViewModel | null | undefined
  >();

  useEffect(() => {
    if (!version) {
      setChangelog(null);

      return;
    }

    let cancelled = false;

    setChangelog(undefined);
    BApi.changelog
      .getChangelog({ version })
      .then((rsp) => {
        if (!cancelled) setChangelog(rsp.data ?? null);
      })
      .catch(() => {
        if (!cancelled) setChangelog(null);
      });

    return () => {
      cancelled = true;
    };
  }, [version]);

  if (changelog === undefined) {
    return (
      <div className="flex items-center justify-center py-10">
        <Spinner size="sm" />
      </div>
    );
  }

  const githubUrl = changelog?.htmlUrl ?? fallbackUrl ?? RELEASES_URL;

  if (changelog === null) {
    return (
      <div className="flex flex-col items-center gap-2 py-10 text-sm text-foreground-500">
        <span>{t<string>("changelog.unavailable")}</span>
        <ExternalLink href={githubUrl}>{t<string>("changelog.viewOnGithub")}</ExternalLink>
      </div>
    );
  }

  return (
    <div className="flex flex-col gap-3">
      <ChangelogMarkdown>{changelog.markdown}</ChangelogMarkdown>
      <div className="flex justify-end border-t border-default-200 pt-2">
        <ExternalLink href={githubUrl} size="sm">
          {t<string>("changelog.viewOnGithub")}
        </ExternalLink>
      </div>
    </div>
  );
};

/** Opens a version's notes in a modal. */
export const useChangelogModal = () => {
  const { t } = useTranslation();
  const { createPortal } = useBakabaseContext();

  return (version?: string, fallbackUrl?: string) => {
    createPortal(Modal, {
      size: "xl",
      title: version
        ? t<string>("changelog.titleWithVersion", { version })
        : t<string>("changelog.title"),
      defaultVisible: true,
      children: <ChangelogViewer fallbackUrl={fallbackUrl} version={version} />,
      footer: { actions: ["cancel"] },
    });
  };
};

interface ChangelogButtonProps {
  version?: string;
  /** Icon-only, for the sidebar where a label does not fit. */
  isIconOnly?: boolean;
  size?: "sm" | "md";
  className?: string;
}

/**
 * The "what changed" affordance that sits next to every update action, so a
 * user is never asked to install a version they cannot read about first.
 */
export const ChangelogButton = ({
  version,
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
      onPress={() => showChangelog(version)}
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
