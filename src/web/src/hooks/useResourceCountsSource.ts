import { useSyncExternalStore } from "react";

type Counts = Record<string, number> | undefined;
export type ResourceCountsSource = {
  getSnapshot: () => Counts;
  subscribe: (listener: () => void) => () => void;
};

/** A stable source keeps editors mounted in a portal connected to updated counts. */
export function createResourceCountsSource() {
  let snapshot: Counts;
  const listeners = new Set<() => void>();

  return {
    getSnapshot: () => snapshot,
    subscribe: (listener: () => void) => {
      listeners.add(listener);

      return () => {
        listeners.delete(listener);
      };
    },
    publish: (counts: Counts) => {
      if (counts === snapshot) return;
      snapshot = counts;
      listeners.forEach((listener) => listener());
    },
  };
}

const emptySource = createResourceCountsSource();

export function useResourceCountsSource(source?: ResourceCountsSource, fallback?: Counts) {
  const current = source ?? emptySource;
  const counts = useSyncExternalStore(current.subscribe, current.getSnapshot, current.getSnapshot);

  return source ? counts : fallback;
}
