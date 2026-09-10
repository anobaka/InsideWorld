"use client";

import type { ClientLogEntry, ClientLogPage } from "@/core/clientApi";

import { useCallback, useEffect, useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { FolderOpenOutlined } from "@ant-design/icons";
import { AiOutlineReload } from "react-icons/ai";

import { clientApi } from "@/core/clientApi";
import {
  Button,
  Input,
  Modal,
  Select,
  Snippet,
  Table,
  TableBody,
  TableCell,
  TableColumn,
  TableHeader,
  TableRow,
} from "@/components/bakaui";
import { useBakabaseContext } from "@/components/ContextProvider/BakabaseContextProvider";

/**
 * Serilog's level names, which are not the server's.
 *
 * The server's table stores Microsoft's LogLevel — Trace, Critical — while the file sink
 * writes Serilog's own — Verbose, Fatal. Reusing the server's enum here would offer
 * filters that match nothing in the file.
 */
const LEVELS = ["Verbose", "Debug", "Information", "Warning", "Error", "Fatal"];

/** How many lines to keep on screen. Capped again by the client. */
const TAKES = [200, 500, 2000];

const levelClass = (level?: string) => {
  switch (level) {
    case "Error":
    case "Fatal":
      return "text-danger";
    case "Warning":
      return "text-warning";
    case "Verbose":
    case "Debug":
      return "text-foreground-400";
    default:
      return "text-foreground-600";
  }
};

/**
 * The client's own log.
 *
 * Everything else on this page is the server's, forwarded. This is the only place a
 * failed handshake, a refused signature or a player that would not start is written
 * down — none of those ever reach the server to be recorded there.
 */
const ClientLog = () => {
  const { t } = useTranslation();
  const { createPortal } = useBakabaseContext();

  const [page, setPage] = useState<ClientLogPage>();
  const [failed, setFailed] = useState(false);
  const [loading, setLoading] = useState(false);
  const [level, setLevel] = useState<string>();
  const [contains, setContains] = useState("");
  const [take, setTake] = useState<number>(TAKES[0]);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setPage(await clientApi.log({ take, level, contains: contains.trim() || undefined }));
      setFailed(false);
    } catch {
      setFailed(true);
    } finally {
      setLoading(false);
    }
  }, [take, level, contains]);

  // Typing in the message box does not re-fetch on every keystroke — that box is
  // submitted with Enter or the refresh button.
  useEffect(() => {
    load();
  }, [take, level]);

  const expand = (entry: ClientLogEntry) =>
    createPortal(Modal, {
      size: "xl",
      title: t("log.label.log"),
      defaultVisible: true,
      children: (
        <pre className="whitespace-pre-wrap break-all text-xs">
          {[entry.timestamp, entry.level, entry.source].filter(Boolean).join(" · ")}
          {"\n\n"}
          {entry.message}
        </pre>
      ),
      footer: { actions: ["cancel"] },
    });

  const rows = useMemo(
    () => (page?.entries ?? []).map((entry, index) => ({ ...entry, id: index })),
    [page],
  );

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center gap-3">
        <div className="flex items-center gap-1">
          <span className="font-medium">{t<string>("log.filter.level")}</span>
          <Select
            className="min-w-[130px]"
            dataSource={LEVELS.map((l) => ({ label: l, value: l }))}
            placeholder={t<string>("log.filter.all")}
            selectedKeys={level ? new Set([level]) : new Set()}
            size="sm"
            onSelectionChange={(keys) => setLevel((Array.from(keys)[0] as string) || undefined)}
          />
        </div>
        <div className="flex items-center gap-1">
          <span className="font-medium">{t<string>("log.filter.message")}</span>
          <Input
            className="min-w-[180px]"
            placeholder={t<string>("log.client.filter.containsPlaceholder")}
            size="sm"
            value={contains}
            onKeyDown={(e) => {
              if (e.key === "Enter") {
                load();
              }
            }}
            onValueChange={setContains}
          />
        </div>
        <div className="flex items-center gap-1">
          <span className="font-medium">{t<string>("log.client.filter.lines")}</span>
          <Select
            className="min-w-[100px]"
            dataSource={TAKES.map((n) => ({ label: String(n), value: String(n) }))}
            selectedKeys={new Set([String(take)])}
            size="sm"
            onSelectionChange={(keys) => setTake(Number(Array.from(keys)[0] ?? TAKES[0]))}
          />
        </div>
        <Button isLoading={loading} size="sm" onPress={load}>
          <AiOutlineReload className="text-base" />
          {t<string>("log.client.action.refresh")}
        </Button>
      </div>

      {page?.directory && (
        <div className="flex flex-wrap items-center gap-1 rounded-medium border border-default-200 dark:border-default-100 px-3 py-2">
          <span className="text-sm text-foreground-500">{t<string>("log.client.directory")}</span>
          <Snippet hideSymbol size="sm" variant="bordered">
            {page.directory}
          </Snippet>
          {/* Not the shared open-folder button: that one is a forwarded route, and it
              would translate this path as though it were the server's — which for a
              directory on this very disk means refusing to open it. */}
          <Button
            isIconOnly
            color="primary"
            size="sm"
            variant="light"
            onPress={() => clientApi.openLogDirectory()}
          >
            <FolderOpenOutlined className="text-base" />
          </Button>
        </div>
      )}

      {failed && <div className="text-sm text-danger">{t<string>("log.client.unavailable")}</div>}

      {!failed && page && !page.available && (
        <div className="text-sm text-foreground-400">{t<string>("log.client.empty")}</div>
      )}

      <Table removeWrapper aria-label="client log" selectionMode="none" size="sm">
        <TableHeader>
          <TableColumn>{t<string>("log.filter.time")}</TableColumn>
          <TableColumn>{t<string>("log.filter.level")}</TableColumn>
          <TableColumn>{t<string>("log.client.source")}</TableColumn>
          <TableColumn>{t<string>("log.filter.message")}</TableColumn>
        </TableHeader>
        <TableBody emptyContent={t<string>("log.client.noEntries")} items={rows}>
          {(entry) => (
            <TableRow key={entry.id}>
              {/* Shown as written rather than reformatted into the browser's locale: the
                  offset in the line is the client machine's, and a clock difference is
                  itself a cause of refused signatures. */}
              <TableCell className="whitespace-nowrap text-xs">{entry.timestamp ?? "-"}</TableCell>
              <TableCell className={`text-xs ${levelClass(entry.level)}`}>
                {entry.level ?? "-"}
              </TableCell>
              <TableCell className="max-w-[240px] truncate text-xs" title={entry.source}>
                {entry.source && entry.source !== "." ? entry.source : "-"}
              </TableCell>
              <TableCell className="max-w-[520px] text-xs">
                {entry.message.length > 120 || entry.message.includes("\n") ? (
                  <span className="flex items-center gap-1">
                    <pre className="inline truncate">
                      {entry.message.split("\n")[0].slice(0, 120)}
                    </pre>
                    <Button color="primary" size="sm" variant="light" onPress={() => expand(entry)}>
                      {t<string>("log.action.expand")}
                    </Button>
                  </span>
                ) : (
                  <pre className="inline whitespace-pre-wrap break-all">{entry.message}</pre>
                )}
              </TableCell>
            </TableRow>
          )}
        </TableBody>
      </Table>
    </div>
  );
};

ClientLog.displayName = "ClientLog";

export default ClientLog;
