import type {
  ClientPairingOutcome,
  RemoteDevicePlatform,
  ServerHandshakeOutcome,
} from "@/sdk/constants";

/**
 * The thin client's own API, under `/client/`.
 *
 * Hand-written rather than generated, because `gen-sdk` builds from the server's
 * swagger and these endpoints are not the server's — they exist only in the client
 * process, answering questions about this machine. The enums below still come from
 * `constants.ts`: the codegen driver sees both sides and emits them, so the one thing
 * that could silently drift does not.
 *
 * Every call here is served by the forwarding layer on this origin. In any other
 * flavour they simply do not exist, which is why the pages that use them are shown
 * only when `clientMode` says PureClient.
 */

export interface ClientPathMapping {
  serverPath: string;
  localPath: string;
}

export interface ClientKnownServer {
  serverId: string;
  serverName?: string;
  baseAddress: string;
  pairedAt: string;
  lastConnectedAt?: string;
  deviceId: string;
  isActive: boolean;
  pathMappings: ClientPathMapping[];
}

export interface ClientStatus {
  clientVersion: string;
  deviceName: string;
  platform: RemoteDevicePlatform;
  activeServerId?: string;
  serverReachable: boolean;
  /** Route keys this build can run here, e.g. `GET /tool/open`. */
  implementedUserMachineRoutes: string[];
  servers: ClientKnownServer[];
}

export interface ClientHandshakeResult {
  outcome: ServerHandshakeOutcome;
  detail?: string;
  server?: {
    id: string;
    name: string;
    appVersion: string;
    mode: number;
    pairingSupported: boolean;
  };
}

export interface ClientPairingResult {
  outcome: ClientPairingOutcome;
  serverId?: string;
  serverName?: string;
  detail?: string;
}

export interface ClientPairingTicket {
  outcome: ClientPairingOutcome;
  requestId?: string;
  expiresAt?: string;
}

const call = async <T>(path: string, init?: RequestInit): Promise<T> => {
  const rsp = await fetch(`/client${path}`, {
    ...init,
    headers: init?.body ? { "Content-Type": "application/json", ...init?.headers } : init?.headers,
  });

  if (!rsp.ok) {
    throw new Error(`${init?.method ?? "GET"} /client${path} failed with ${rsp.status}`);
  }

  const envelope = await rsp.json();

  return envelope.data as T;
};

const post = <T>(path: string, body?: unknown) =>
  call<T>(path, { method: "POST", body: body === undefined ? undefined : JSON.stringify(body) });

export const clientApi = {
  status: () => call<ClientStatus>("/status"),

  /** Asks an address what it is. Never throws for an unreachable server — that is an answer. */
  connect: (address: string) => post<ClientHandshakeResult>("/connect", { address }),

  pairWithCode: (address: string, code: string) =>
    post<ClientPairingResult>("/pair/code", { address, code }),

  /** Files a request for an already-paired device to approve. */
  requestPairing: (address: string) => post<ClientPairingTicket>("/pair/request", { address }),

  /** Collects the credentials an approval produced. Answers "not yet" until somebody acts. */
  claimPairing: (address: string, requestId: string) =>
    post<ClientPairingResult>("/pair/claim", { address, requestId }),

  activateServer: (serverId: string) =>
    post<{ changed: boolean }>(`/servers/${encodeURIComponent(serverId)}/activate`),

  forgetServer: (serverId: string) =>
    call<{ changed: boolean }>(`/servers/${encodeURIComponent(serverId)}`, { method: "DELETE" }),

  setPathMappings: (serverId: string, mappings: ClientPathMapping[]) =>
    call<{ changed: boolean }>(`/servers/${encodeURIComponent(serverId)}/path-mappings`, {
      method: "PUT",
      body: JSON.stringify({ mappings }),
    }),
};
