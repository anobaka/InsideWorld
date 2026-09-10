import { useSyncExternalStore } from "react";

export type ChoiceResourceCountsState = {
  counts?: Record<string, number>;
  loading?: boolean;
  error?: boolean;
  stale?: boolean;
};

export type ChoiceResourceCountsStore = {
  getSnapshot: () => ChoiceResourceCountsState;
  subscribe: (listener: () => void) => () => void;
};

/** Filter-owned state; extra-label components keep this subscription inside open editors. */
export function createChoiceResourceCountsStore() {
  let snapshot: ChoiceResourceCountsState = {};
  const listeners = new Set<() => void>();

  return {
    getSnapshot: () => snapshot,
    subscribe: (listener: () => void) => {
      listeners.add(listener);

      return () => {
        listeners.delete(listener);
      };
    },
    publish: (state: ChoiceResourceCountsState) => {
      if (snapshot === state) return;
      snapshot = state;
      listeners.forEach((listener) => listener());
    },
  };
}

export function useChoiceResourceCounts(store: ChoiceResourceCountsStore) {
  return useSyncExternalStore(store.subscribe, store.getSnapshot, store.getSnapshot);
}
