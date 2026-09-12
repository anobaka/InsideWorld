"use client";

import { useState } from "react";
import { useTranslation } from "react-i18next";

import ClientLog from "./ClientLog";
import ServerLog from "./ServerLog";

import { Tab, Tabs } from "@/components/bakaui";
import { useIsPureClient, useRemoteAccessStore } from "@/stores/remoteAccess";

type LogSource = "server" | "client";

/**
 * The log page, which in a thin client is two logs.
 *
 * Only one of them is ever the answer to a given question, and which one is not obvious
 * from the symptom: a library that will not scan is the server's, a player that will not
 * start is the client's, and "nothing happens when I click this" could be either. So both
 * are here, named for the machine they came from rather than for what they contain.
 */
export default function LogPage() {
  const { t } = useTranslation();
  const isPureClient = useIsPureClient();
  const serverReachable = useRemoteAccessStore((state) => state.serverReachable);
  const [chosen, setChosen] = useState<LogSource>();

  // Until the user picks, show whichever side can actually answer. A client that has lost
  // its server cannot fetch the server's log at all, and opening on an empty table with a
  // network error is a worse first screen than the log that explains the disconnection.
  const source: LogSource = chosen ?? (serverReachable ? "server" : "client");

  if (!isPureClient) {
    return (
      <div className="p-4">
        <ServerLog />
      </div>
    );
  }

  return (
    <div className="flex flex-col gap-3 p-4">
      <Tabs selectedKey={source} size="sm" onSelectionChange={(key) => setChosen(key as LogSource)}>
        <Tab key="server" title={t<string>("log.source.server")} />
        <Tab key="client" title={t<string>("log.source.client")} />
      </Tabs>

      {source === "server" ? <ServerLog /> : <ClientLog />}
    </div>
  );
}
