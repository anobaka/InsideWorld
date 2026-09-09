import { create } from "zustand";

import { ClientMode, RemoteAccessMode } from "@/sdk/constants";
import BApi from "@/sdk/BApi";

interface IRemoteAccessState {
  /** False until the first answer from the server arrives. */
  initialized: boolean;
  /**
   * Whether this browser is on the machine running Bakabase. Answered by the
   * server from the connection itself, not guessed from the URL — opening
   * `http://192.168.1.5:34567` on the host is still local, and a reverse proxy
   * would make any URL-based guess wrong anyway.
   *
   * Not the same question as {@link clientMode}. A thin client is not local —
   * its files really are elsewhere — yet it can still launch a player.
   */
  isLocal: boolean;
  mode: RemoteAccessMode;
  /**
   * Which flavour is answering. A thin client's forwarding layer answers this
   * endpoint itself, which is the only way `PureClient` ever appears.
   */
  clientMode: ClientMode;
  /**
   * False while a thin client cannot reach its server. Always true from a
   * server, which answered by definition.
   */
  serverReachable: boolean;
  /** Whether a sign-in capture window can open for this caller. */
  cookieCaptureAvailable: boolean;
  serverName?: string;
  load: () => Promise<void>;
}

export const useRemoteAccessStore = create<IRemoteAccessState>((set) => ({
  initialized: false,
  // Assume local until told otherwise: the desktop app is the overwhelmingly
  // common case, and it must not flicker through a "remote" rendering on start.
  isLocal: true,
  mode: RemoteAccessMode.Disabled,
  clientMode: ClientMode.AllInOne,
  serverReachable: true,
  cookieCaptureAvailable: true,
  load: async () => {
    try {
      const rsp = await BApi.remoteAccess.getRemoteAccessContext();
      const data = rsp.data;

      if (data) {
        const isLocal = data.isLocal ?? true;

        set({
          initialized: true,
          isLocal,
          mode: data.mode ?? RemoteAccessMode.Disabled,
          // A backend that predates the field is an all-in-one when the caller
          // is on it and an ordinary remote browser otherwise. Neither can be
          // PureClient — that answer only ever comes from a client that
          // intercepts this endpoint.
          clientMode: data.clientMode ?? (isLocal ? ClientMode.AllInOne : ClientMode.RemoteBrowser),
          serverReachable: data.serverReachable ?? true,
          cookieCaptureAvailable: data.cookieCaptureAvailable ?? isLocal,
          serverName: data.serverName ?? undefined,
        });
      }
    } catch {
      // An older backend, or a request that failed on a flaky LAN. Staying
      // local-by-default keeps the desktop app working; a genuinely remote
      // device would have been refused by the gate long before this point.
      set({ initialized: true });
    }
  },
}));

/**
 * True when the UI is being used from another device, so the files it shows are
 * not on this machine.
 *
 * This asks about *files*, not about *actions* — a thin client is a remote
 * client by this measure and can still launch a player. Use
 * {@link useUserSideActionsRunHere} for anything that runs a program, opens a
 * folder, or shows a window.
 */
export const useIsRemoteClient = () =>
  useRemoteAccessStore((state) => state.initialized && !state.isLocal);

/**
 * True when an action that has to happen on a person's own machine will happen
 * on *this* one.
 *
 * Two flavours qualify and they qualify for different reasons: the all-in-one,
 * because the server is this machine; the thin client, because it intercepts
 * those endpoints and runs them here. An ordinary browser pointed at a server
 * qualifies for neither, and there the action would land on a screen nobody is
 * watching.
 */
export const useUserSideActionsRunHere = () =>
  useRemoteAccessStore(
    (state) => state.initialized && (state.isLocal || state.clientMode === ClientMode.PureClient),
  );

/** True in the thin client specifically. */
export const useIsPureClient = () =>
  useRemoteAccessStore((state) => state.initialized && state.clientMode === ClientMode.PureClient);

/**
 * True when a sign-in capture window can open. In the thin client that window
 * belongs to this process, so it opens here with this machine's browser and
 * this person's cookies — which is why the client reports it available even
 * though its server is elsewhere.
 */
export const useCookieCaptureAvailable = () =>
  useRemoteAccessStore((state) => state.initialized && state.cookieCaptureAvailable);
