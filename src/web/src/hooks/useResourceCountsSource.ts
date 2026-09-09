import { useSyncExternalStore } from "react";

type Counts = Record<string, number> | undefined;
export type ResourceCountsPresentation = {
  /** Stable count-slot widths in ch, including parentheses. */
  reservedWidths?: Readonly<Record<string, number>>;
  loading?: boolean;
  error?: boolean;
  stale?: boolean;
};
export type ResourceCountsSource = {
  getSnapshot: () => Counts;
  getPresentationSnapshot: () => ResourceCountsPresentation | undefined;
  subscribe: (listener: () => void) => () => void;
};

/** A stable source keeps editors mounted in a portal connected to updated counts. */
export function createResourceCountsSource() {
  let snapshot: Counts;
  let presentation: ResourceCountsPresentation | undefined;
  const listeners = new Set<() => void>();

  return {
    getSnapshot: () => snapshot,
    getPresentationSnapshot: () => presentation,
    subscribe: (listener: () => void) => {
      listeners.add(listener);

      return () => {
        listeners.delete(listener);
      };
    },
    publish: (counts: Counts, nextPresentation?: ResourceCountsPresentation) => {
      if (counts === snapshot && presentation === nextPresentation) return;
      snapshot = counts;
      presentation = nextPresentation;
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

/** Presentation travels with counts so selectors opened in a portal stay in sync. */
export function useResourceCountsPresentation(source?: ResourceCountsSource) {
  const current = source ?? emptySource;

  return useSyncExternalStore(
    current.subscribe,
    current.getPresentationSnapshot,
    current.getPresentationSnapshot,
  );
}
