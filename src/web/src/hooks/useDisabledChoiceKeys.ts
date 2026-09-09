import { useSyncExternalStore } from "react";

type DisabledKeys = ReadonlySet<string> | undefined;

export type DisabledChoiceKeysSource = {
  getSnapshot: () => DisabledKeys;
  subscribe: (listener: () => void) => () => void;
};

/** Keeps choice availability current in editors mounted in a separate portal. */
export function createDisabledChoiceKeysSource() {
  let snapshot: DisabledKeys;
  const listeners = new Set<() => void>();

  return {
    getSnapshot: () => snapshot,
    subscribe: (listener: () => void) => {
      listeners.add(listener);

      return () => {
        listeners.delete(listener);
      };
    },
    publish: (keys: DisabledKeys) => {
      if (keys === snapshot) return;
      snapshot = keys;
      listeners.forEach((listener) => listener());
    },
  };
}

const emptySource = createDisabledChoiceKeysSource();

export function useDisabledChoiceKeys(source?: DisabledChoiceKeysSource, fallback?: DisabledKeys) {
  const current = source ?? emptySource;
  const keys = useSyncExternalStore(current.subscribe, current.getSnapshot, current.getSnapshot);

  return source ? keys : fallback;
}
