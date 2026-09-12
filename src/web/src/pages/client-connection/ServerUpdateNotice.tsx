"use client";

import type { BakabaseInfrastructuresComponentsAppUpgradeAbstractionsAppVersionInfo } from "@/sdk/Api";

import React, { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import Markdown from "react-markdown";

import BApi from "@/sdk/BApi";
import { UpdaterStatus } from "@/sdk/constants";
import { Button, Chip, Modal, Progress } from "@/components/bakaui";
import ExternalLink from "@/components/ExternalLink";
import { useAppUpdaterStateStore } from "@/stores/appUpdaterState";
import { useBakabaseContext } from "@/components/ContextProvider/BakabaseContextProvider";

/**
 * A new version of the server, offered where deciding about it makes sense.
 *
 * The sidebar banner in a thin client is about the client, and rightly so — it updates
 * the program in front of the user. The server is a different machine that other people
 * may be using, so its update is never started automatically and never from a banner
 * that looks like it is about this window: it is shown here, on the page about the
 * connection, with the server named.
 */
const ServerUpdateNotice: React.FC<{ serverName?: string }> = ({ serverName }) => {
  const { t } = useTranslation();
  const { createPortal } = useBakabaseContext();
  const updaterState = useAppUpdaterStateStore((state) => state);

  const [newVersion, setNewVersion] =
    useState<BakabaseInfrastructuresComponentsAppUpgradeAbstractionsAppVersionInfo>();

  useEffect(() => {
    BApi.updater
      .getNewAppVersion()
      .then((rsp) => setNewVersion(rsp.data))
      .catch(() => {});
  }, []);

  const { status } = updaterState;
  const name = serverName ?? t<string>("clientConnection.serverUpdate.theServer");

  if (status === UpdaterStatus.Running) {
    return (
      <Notice>
        <Progress
          showValueLabel
          className="w-[220px]"
          label={t("clientConnection.serverUpdate.downloading", {
            name,
            version: newVersion?.version ?? "",
          })}
          size="sm"
          value={updaterState.percentage}
        />
      </Notice>
    );
  }

  if (status === UpdaterStatus.PendingRestart) {
    return (
      <Notice>
        <span className="text-sm">
          {t("clientConnection.serverUpdate.pendingRestart", { name })}
        </span>
        <Button color="primary" size="sm" onPress={() => BApi.updater.restartAndUpdateApp()}>
          {t<string>("clientConnection.serverUpdate.restart")}
        </Button>
      </Notice>
    );
  }

  if (!newVersion?.version) {
    return null;
  }

  return (
    <Notice>
      <span className="text-sm">{t("clientConnection.serverUpdate.available", { name })}</span>
      <Chip radius="sm" size="sm" variant="flat">
        {newVersion.version}
      </Chip>
      {newVersion.changelog && (
        <Button
          color="secondary"
          size="sm"
          variant="light"
          onPress={() =>
            createPortal(Modal, {
              size: "xl",
              title: newVersion.version,
              defaultVisible: true,
              children: (
                <Markdown
                  components={{ a: (props) => <ExternalLink {...props} target="_blank" /> }}
                >
                  {newVersion.changelog}
                </Markdown>
              ),
              footer: { actions: ["cancel"] },
            })
          }
        >
          {t<string>("clientConnection.serverUpdate.changelog")}
        </Button>
      )}
      <Button
        color="success"
        size="sm"
        variant="flat"
        onPress={() => BApi.updater.startUpdatingApp()}
      >
        {t<string>("clientConnection.serverUpdate.start")}
      </Button>
    </Notice>
  );
};

const Notice: React.FC<{ children: React.ReactNode }> = ({ children }) => (
  <div className="flex flex-wrap items-center gap-2 rounded-medium border border-default-200 dark:border-default-100 px-3 py-2">
    {children}
  </div>
);

ServerUpdateNotice.displayName = "ServerUpdateNotice";

export default ServerUpdateNotice;
