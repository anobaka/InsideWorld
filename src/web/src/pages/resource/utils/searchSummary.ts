import type { SearchFilter, SearchFilterGroup } from "@/components/ResourceFilter/models";
import type { SearchForm } from "@/pages/resource/models";

import { SearchOperation } from "@/sdk/constants";

/**
 * True when a filter actually narrows the search.
 *
 * A filter the user picked a property for but never gave a value to is dropped
 * server-side (`ResourceSearchExtensions.IsValid`), so summarizing it would
 * describe a criterion that never ran — "Genre in", with nothing after it.
 * `IsNull` / `IsNotNull` are the two operations that carry no value by design.
 */
export const filterIsEffective = (filter: SearchFilter): boolean => {
  if (filter.propertyId == undefined || filter.propertyPool == undefined) return false;
  if (filter.operation == undefined) return false;

  if (
    filter.operation === SearchOperation.IsNull ||
    filter.operation === SearchOperation.IsNotNull
  ) {
    return true;
  }

  return !!filter.dbValue || !!filter.bizValue;
};

/** The filters of a group that are worth summarizing, in their original order. */
export const effectiveFiltersOf = (group: SearchFilterGroup): SearchFilter[] =>
  (group.filters ?? []).filter(filterIsEffective);

/** True when the group, or any group nested under it, holds an effective filter. */
export const groupHasContent = (group: SearchFilterGroup): boolean =>
  effectiveFiltersOf(group).length > 0 || (group.groups ?? []).some(groupHasContent);

/** True when the form carries anything worth summarizing in the tab tooltip. */
export const hasSearchSummary = (form: SearchForm | undefined): boolean => {
  if (!form) return false;

  return (
    !!form.keyword ||
    !!form.tags?.length ||
    !!form.orders?.length ||
    (!!form.group && groupHasContent(form.group))
  );
};
