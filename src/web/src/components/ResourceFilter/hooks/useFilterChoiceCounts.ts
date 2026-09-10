import type { ReferenceProperty } from "@/components/Property/referenceValues";
import type { SearchForm } from "@/pages/resource/models";

import { useEffect, useMemo, useState } from "react";

import { createChoiceResourceCountsStore } from "./choiceResourceCountsStore";

import {
  referenceValueCountsCriteria,
  useReferenceValueResourceCounts,
} from "@/hooks/useReferenceValueResourceCounts";

const emptyWidths: Readonly<Record<string, number>> = {};

/** Global totals bound filtered counts; keep slots from shrinking during consecutive edits. */
function reserveWidths(
  previous: Readonly<Record<string, number>>,
  ...counts: (Record<string, number> | undefined)[]
): Readonly<Record<string, number>> {
  let updated: Record<string, number> | undefined;

  for (const values of counts) {
    for (const [id, count] of Object.entries(values ?? {})) {
      if (count <= 0) continue;
      const width = count.toLocaleString().length + 2;

      if (width <= ((updated ?? previous)[id] ?? 0)) continue;
      updated ??= { ...previous };
      updated[id] = width;
    }
  }

  return updated ?? previous;
}

/** Resource-count display and availability policies belong to the filter using the options. */
export function useFilterChoiceCounts(property: ReferenceProperty, search?: SearchForm) {
  const globalUsage = useReferenceValueResourceCounts(property);
  const hasCriteria = JSON.stringify(referenceValueCountsCriteria(search)) !== "{}";
  const filteredUsage = useReferenceValueResourceCounts(hasCriteria ? property : undefined, search);
  const usage = hasCriteria ? filteredUsage : globalUsage;
  const propertyKey = `${property.pool}:${property.id}:${property.type}`;
  const [retained, setRetained] = useState<{
    key: string;
    counts?: Record<string, number>;
    widths: Readonly<Record<string, number>>;
  }>();
  const previous = retained?.key === propertyKey ? retained : undefined;
  // When clearing/reapplying criteria, use the last displayed result rather than an older
  // filtered hook result. A different property never inherits these counts or widths.
  const counts = usage.counts && !usage.stale ? usage.counts : (previous?.counts ?? usage.counts);
  const reservedWidths = useMemo(
    () => reserveWidths(previous?.widths ?? emptyWidths, globalUsage.counts, counts),
    [previous?.widths, globalUsage.counts, counts],
  );

  useEffect(() => {
    if (
      retained?.key !== propertyKey ||
      retained.counts !== counts ||
      retained.widths !== reservedWidths
    ) {
      setRetained({ key: propertyKey, counts, widths: reservedWidths });
    }
  }, [propertyKey, counts, reservedWidths, retained]);

  const state = useMemo(
    () => ({
      counts,
      reservedWidths,
      loading: usage.loading,
      error: usage.error,
      stale: counts !== undefined && (usage.stale || counts !== usage.counts),
    }),
    [counts, reservedWidths, usage.loading, usage.error, usage.stale, usage.counts],
  );
  const store = useMemo(createChoiceResourceCountsStore, [propertyKey]);

  useEffect(() => store.publish(state), [store, state]);

  // Current-filter zero counts can still be valid replacements or OR alternatives.
  const disabledKeys = useMemo(
    () =>
      globalUsage.counts === undefined
        ? undefined
        : new Set(Object.keys(globalUsage.counts).filter((id) => globalUsage.counts![id] === 0)),
    [globalUsage.counts],
  );

  return { store, disabledKeys, propertyKey, hasCounts: counts !== undefined };
}
