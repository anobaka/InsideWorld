"use client";

import type { ClientPathMapping, ClientStatus } from "@/core/clientApi";

import { useCallback, useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import toast from "react-hot-toast";
import { AiOutlineDelete, AiOutlinePlus } from "react-icons/ai";

import { clientApi } from "@/core/clientApi";
import { Button, Input } from "@/components/bakaui";
import { useIsPureClient } from "@/stores/remoteAccess";

/**
 * Where the active server's libraries are on this machine.
 *
 * Nothing can infer this — a container serving `/data/media` and a desktop mounting the
 * same share as `Z:\media` have no way to discover each other's names — so it is
 * configuration, and until a library is listed here the client refuses to open its files
 * rather than guessing at a location.
 */
const ClientPathMappingPage = () => {
  const { t } = useTranslation();
  const isPureClient = useIsPureClient();

  const [status, setStatus] = useState<ClientStatus>();
  const [rows, setRows] = useState<ClientPathMapping[]>([]);
  const [saving, setSaving] = useState(false);

  const load = useCallback(async () => {
    try {
      const fresh = await clientApi.status();

      setStatus(fresh);
      setRows(fresh.servers.find((s) => s.isActive)?.pathMappings ?? []);
    } catch {
      setStatus(undefined);
    }
  }, []);

  useEffect(() => {
    if (isPureClient) {
      load();
    }
  }, [isPureClient, load]);

  if (!isPureClient) {
    return (
      <div className="p-4 text-sm text-foreground-400">{t("clientPathMapping.onlyInClient")}</div>
    );
  }

  const active = status?.servers.find((s) => s.isActive);

  if (!active) {
    return <div className="p-4 text-sm text-foreground-400">{t("clientPathMapping.noServer")}</div>;
  }

  const update = (index: number, patch: Partial<ClientPathMapping>) =>
    setRows((current) => current.map((r, i) => (i === index ? { ...r, ...patch } : r)));

  const save = async () => {
    setSaving(true);
    try {
      // Sent whole rather than merged: a row the user deleted has to disappear, and an
      // append-only save would leave it mapping.
      await clientApi.setPathMappings(
        active.serverId,
        rows.filter((r) => r.serverPath.trim() && r.localPath.trim()),
      );
      toast.success(t("common.success.saved"));
      await load();
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="flex flex-col gap-4 p-4">
      <div className="flex flex-col gap-1">
        <span className="text-sm font-medium">
          {t("clientPathMapping.title", { name: active.serverName ?? active.serverId })}
        </span>
        <span className="text-xs text-foreground-400">{t("clientPathMapping.description")}</span>
      </div>

      <div className="flex flex-col gap-2">
        {rows.map((row, index) => (
          <div key={index} className="flex items-end gap-2">
            <Input
              className="max-w-[360px]"
              label={index === 0 ? t("clientPathMapping.serverPath") : undefined}
              placeholder="/data/media"
              value={row.serverPath}
              onValueChange={(v) => update(index, { serverPath: v })}
            />
            <Input
              className="max-w-[360px]"
              label={index === 0 ? t("clientPathMapping.localPath") : undefined}
              placeholder="Z:\media"
              value={row.localPath}
              onValueChange={(v) => update(index, { localPath: v })}
            />
            <Button
              isIconOnly
              color="danger"
              variant="light"
              onPress={() => setRows((current) => current.filter((_, i) => i !== index))}
            >
              <AiOutlineDelete />
            </Button>
          </div>
        ))}

        <div className="flex gap-2">
          <Button
            size="sm"
            startContent={<AiOutlinePlus />}
            variant="flat"
            onPress={() => setRows((current) => [...current, { serverPath: "", localPath: "" }])}
          >
            {t("clientPathMapping.add")}
          </Button>
          <Button color="primary" isLoading={saving} size="sm" onPress={save}>
            {t("common.save")}
          </Button>
        </div>
      </div>

      <div className="flex flex-col gap-1">
        <span className="text-sm font-medium">{t("clientPathMapping.canRunHere")}</span>
        <span className="text-xs text-foreground-400">{t("clientPathMapping.canRunHereTip")}</span>
        <div className="flex flex-col gap-0.5">
          {status?.implementedUserMachineRoutes.map((route) => (
            <span key={route} className="text-xs font-mono text-foreground-500">
              {route}
            </span>
          ))}
        </div>
      </div>
    </div>
  );
};

ClientPathMappingPage.displayName = "ClientPathMappingPage";

export default ClientPathMappingPage;
