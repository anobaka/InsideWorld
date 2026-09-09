import type { Props } from "@/components/Property/components/PropertyValueRenderer";

import { useTranslation } from "react-i18next";
import { useEffect, useMemo } from "react";

import PropertyValueRenderer from "@/components/Property/components/PropertyValueRenderer";
import { getUnreferencedValueIds } from "@/components/Property/referenceValues";
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
  const { counts } = hasCriteria ? filteredUsage : globalUsage;
  // Portals keep the original source even when the active criteria become empty/nonempty.
  const resourceCountsSource = useMemo(createResourceCountsSource, []);

  useEffect(() => resourceCountsSource.publish(counts), [resourceCountsSource, counts]);
  const explicitDisabledKeys = useDisabledChoiceKeys(props.disabledKeysSource, props.disabledKeys);
  const disabledKeys = useMemo(() => {
    const unusedIds = getUnreferencedValueIds(globalUsage.counts);

    if (!unusedIds && !explicitDisabledKeys) return undefined;

    return new Set([...(unusedIds ?? []), ...(explicitDisabledKeys ?? [])]);
  }, [globalUsage.counts, explicitDisabledKeys]);
  const disabledKeysSource = useMemo(createDisabledChoiceKeysSource, []);

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
    </div>
  );
}
