import type { IProperty } from "./models";
import type { SearchForm } from "@/pages/resource/models";

import { PropertyType, SearchOperation, StandardValueType } from "@/sdk/constants";
import { GroupCombinator } from "@/components/ResourceFilter/models";
import { serializeStandardValue } from "@/components/StandardValue/helpers";

export type ReferenceProperty = Pick<IProperty, "id" | "pool" | "type" | "name">;
export type ReferenceValueResourceCounts = Record<string, number>;

/** Reserve global count widths; never shrink slots while a filter stays mounted. */
export function reserveReferenceValueCountWidths(
  previous: Readonly<Record<string, number>>,
  ...counts: (ReferenceValueResourceCounts | undefined)[]
): Readonly<Record<string, number>> {
  let widths = previous;

  for (const values of counts) {
    for (const [id, count] of Object.entries(values ?? {})) {
      if (count <= 0) continue;
      const width = count.toLocaleString().length + 2;

      if (width <= (widths[id] ?? 0)) continue;
      if (widths === previous) widths = { ...previous };
      (widths as Record<string, number>)[id] = width;
    }
  }

  return widths;
}

/** Only explicit global zeros mean an option has no referencing resources. */
export function getUnreferencedValueIds(
  counts?: ReferenceValueResourceCounts,
): ReadonlySet<string> | undefined {
  return counts === undefined
    ? undefined
    : new Set(Object.keys(counts).filter((value) => counts[value] === 0));
}

/** Use reference IDs, never labels, so case handling stays inside the property module. */
export function buildReferenceValueSearch(property: ReferenceProperty, value: string): SearchForm {
  const single = property.type === PropertyType.SingleChoice;

  return {
    page: 1,
    pageSize: 50,
    group: {
      combinator: GroupCombinator.And,
      disabled: false,
      filters: [
        {
          propertyId: property.id,
          propertyPool: property.pool,
          operation: single ? SearchOperation.Equals : SearchOperation.Contains,
          dbValue: serializeStandardValue(
            single ? value : [value],
            single ? StandardValueType.String : StandardValueType.ListString,
          ),
          disabled: false,
        },
      ],
    },
  };
}
