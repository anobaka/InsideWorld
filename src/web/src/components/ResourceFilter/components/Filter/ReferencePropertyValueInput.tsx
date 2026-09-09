import type { Props } from "@/components/Property/components/PropertyValueRenderer";

import { useTranslation } from "react-i18next";
import { useEffect, useMemo, useState } from "react";

import PropertyValueRenderer from "@/components/Property/components/PropertyValueRenderer";
import {
  getUnreferencedValueIds,
  reserveReferenceValueCountWidths,
} from "@/components/Property/referenceValues";
import { ReferenceValueCountsStatus } from "@/components/Property/components/ReferenceValueCount";
import { createResourceCountsSource } from "@/hooks/useResourceCountsSource";
import {
  createDisabledChoiceKeysSource,
  useDisabledChoiceKeys,
} from "@/hooks/useDisabledChoiceKeys";
import {
  referenceValueCountsCriteria,
  useReferenceValueResourceCounts,
  useReferenceValueSearch,
} from "@/hooks/useReferenceValueResourceCounts";

const emptyWidths: Readonly<Record<string, number>> = {};

/** The current search supplies counts; standalone filters display totals. */
export default function ReferencePropertyValueInput(props: Props) {
  const { t } = useTranslation();
  const search = useReferenceValueSearch();
  // A zero inside the current results can still be a valid replacement/OR choice.
  // Only global totals decide availability, and remain stable when filters change.
  const globalUsage = useReferenceValueResourceCounts(props.property);
  const hasCriteria = JSON.stringify(referenceValueCountsCriteria(search)) !== "{}";
  const filteredUsage = useReferenceValueResourceCounts(
    hasCriteria ? props.property : undefined,
    search,
  );
  const usage = hasCriteria ? filteredUsage : globalUsage;
  const propertyKey = `${props.property.pool}:${props.property.id}:${props.property.type}`;
  const [retained, setRetained] = useState<{
    key: string;
    counts?: Record<string, number>;
    widths: Readonly<Record<string, number>>;
  }>();
  const previous = retained?.key === propertyKey ? retained : undefined;
  // Preserve the last displayed result across empty/nonempty criteria, too:
  // a previously used filtered hook may otherwise expose an older stale result.
  const counts = usage.counts && !usage.stale ? usage.counts : (previous?.counts ?? usage.counts);
  const reservedWidths = useMemo(
    () =>
      reserveReferenceValueCountWidths(previous?.widths ?? emptyWidths, globalUsage.counts, counts),
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

  const presentation = useMemo(
    () =>
      counts !== undefined || usage.loading || usage.error
        ? {
            reservedWidths,
            loading: usage.loading,
            error: usage.error,
            stale: counts !== undefined && (usage.stale || counts !== usage.counts),
          }
        : undefined,
    [reservedWidths, usage.loading, usage.error, usage.stale, usage.counts, counts],
  );
  // Portals keep the original source even when the active criteria become empty/nonempty.
  const resourceCountsSource = useMemo(createResourceCountsSource, [propertyKey]);

  useEffect(
    () => resourceCountsSource.publish(counts, presentation),
    [resourceCountsSource, counts, presentation],
  );
  const explicitDisabledKeys = useDisabledChoiceKeys(props.disabledKeysSource, props.disabledKeys);
  const disabledKeys = useMemo(() => {
    const unusedIds = getUnreferencedValueIds(globalUsage.counts);

    if (!unusedIds && !explicitDisabledKeys) return undefined;

    return new Set([...(unusedIds ?? []), ...(explicitDisabledKeys ?? [])]);
  }, [globalUsage.counts, explicitDisabledKeys]);
  const disabledKeysSource = useMemo(createDisabledChoiceKeysSource, [propertyKey]);

  useEffect(() => disabledKeysSource.publish(disabledKeys), [disabledKeysSource, disabledKeys]);

  return (
    <div
      title={
        counts
          ? t(search ? "property.reference.filteredCountsHelp" : "property.reference.countsHelp")
          : undefined
      }
    >
      <PropertyValueRenderer
        {...props}
        disabledKeys={disabledKeys}
        disabledKeysSource={disabledKeysSource}
        resourceCounts={counts}
        resourceCountsSource={resourceCountsSource}
      />
      <ReferenceValueCountsStatus source={resourceCountsSource} />
    </div>
  );
}
