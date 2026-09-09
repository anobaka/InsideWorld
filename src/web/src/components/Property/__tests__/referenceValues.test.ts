import { describe, expect, it } from "vitest";

import { buildReferenceValueSearch } from "../referenceValues";

import { PropertyPool, PropertyType, SearchOperation, StandardValueType } from "@/sdk/constants";
import { deserializeStandardValue } from "@/components/StandardValue/helpers";

describe("reference value quick search", () => {
  it.each([
    PropertyType.SingleChoice,
    PropertyType.MultipleChoice,
    PropertyType.Tags,
    PropertyType.Multilevel,
  ])("searches exact stored IDs for property type %s", (type) => {
    const id = "MixedCase-ID|with\\separators";
    const form = buildReferenceValueSearch(
      { id: 17, pool: PropertyPool.Custom, type, name: "Category" },
      id,
    );
    const filter = form.group!.filters![0];
    const single = type === PropertyType.SingleChoice;

    expect(filter.propertyPool).toBe(PropertyPool.Custom);
    expect(filter.propertyId).toBe(17);
    expect(filter.operation).toBe(single ? SearchOperation.Equals : SearchOperation.Contains);
    expect(
      deserializeStandardValue(
        filter.dbValue ?? null,
        single ? StandardValueType.String : StandardValueType.ListString,
      ),
    ).toEqual(single ? id : [id]);
    expect(filter.bizValue).toBeUndefined();
    expect(form.page).toBe(1);
    expect(form.pageSize).toBe(50);
  });
});
