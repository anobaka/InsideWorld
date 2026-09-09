import type { Props } from "@/components/Property/components/PropertyValueRenderer";

import { useTranslation } from "react-i18next";

import PropertyValueRenderer from "@/components/Property/components/PropertyValueRenderer";
import {
  useReferenceValueResourceCounts,
  useReferenceValueSearch,
} from "@/hooks/useReferenceValueResourceCounts";

/** The current search supplies counts; standalone filters display totals. */
export default function ReferencePropertyValueInput(props: Props) {
  const { t } = useTranslation();
  const search = useReferenceValueSearch();
  const { counts, source } = useReferenceValueResourceCounts(props.property, search);

  return (
    <div
      title={
        counts
          ? t(search ? "property.reference.filteredCountsHelp" : "property.reference.countsHelp")
          : undefined
      }
    >
      <PropertyValueRenderer {...props} resourceCounts={counts} resourceCountsSource={source} />
    </div>
  );
}
