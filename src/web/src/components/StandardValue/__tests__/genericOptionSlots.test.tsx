import type { OptionDisplayProps } from "../OptionDisplayProps";
import type { IProperty } from "@/components/Property/models";
import type * as Utils from "@/components/utils";

import { createRoot } from "react-dom/client";
import { act } from "react-dom/test-utils";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { deserializeStandardValue, serializeStandardValue } from "../helpers";

import PropertyValueRenderer from "@/components/Property/components/PropertyValueRenderer";
import { getBizValueType, getDbValueType } from "@/components/Property/PropertySystem";
import { PropertyPool, PropertyType } from "@/sdk/constants";

const { standardRenderer } = vi.hoisted(() => ({
  standardRenderer: vi.fn((_props: unknown) => null),
}));

vi.mock("@/components/utils", async (importOriginal) => ({
  ...(await importOriginal<typeof Utils>()),
  buildLogger: () => () => {},
}));
vi.mock("@/components/StandardValue", async () => ({
  ...(await import("../helpers")),
  ChoiceValueRenderer: standardRenderer,
  TagsValueRenderer: standardRenderer,
  MultilevelValueRenderer: standardRenderer,
}));
vi.mock("@/components/ResourceFilter/components/ParentResourceValueRenderer", () => ({
  default: () => null,
}));

type RendererProps = OptionDisplayProps & {
  value?: unknown;
  editor?: {
    value?: unknown;
    onValueChange: (dbValue: unknown, bizValue: unknown) => void;
  };
};

const optionId = "choice-id";
const label = "Original, label";
const scenarios = [
  {
    name: "single choice",
    type: PropertyType.SingleChoice,
    options: { choices: [{ value: optionId, label }] },
    dbValue: optionId,
    bizValue: label,
    editorBizValue: [label],
  },
  {
    name: "multiple choices",
    type: PropertyType.MultipleChoice,
    options: { choices: [{ value: optionId, label }] },
    dbValue: [optionId],
    bizValue: [label],
    editorBizValue: [label],
  },
  {
    name: "tags",
    type: PropertyType.Tags,
    options: { tags: [{ value: optionId, group: "Group", name: label }] },
    dbValue: [optionId],
    bizValue: [{ group: "Group", name: label }],
    editorBizValue: [{ group: "Group", name: label }],
  },
  {
    name: "multilevel choices",
    type: PropertyType.Multilevel,
    options: { data: [{ value: optionId, label }] },
    dbValue: [optionId],
    bizValue: [[label]],
    editorBizValue: [[label]],
  },
];

let root: ReturnType<typeof createRoot>;
let container: HTMLDivElement;

beforeEach(() => {
  (globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;
  standardRenderer.mockClear();
  container = document.createElement("div");
  document.body.appendChild(container);
  root = createRoot(container);
});

afterEach(async () => {
  await act(async () => root.unmount());
  container.remove();
});

describe.each(scenarios)("property option slots for $name", (scenario) => {
  it("forwards generic display slots without changing serialized value conversion", async () => {
    const property: IProperty = {
      id: 1,
      name: "Review state",
      pool: PropertyPool.Custom,
      type: scenario.type,
      dbValueType: getDbValueType(scenario.type),
      bizValueType: getBizValueType(scenario.type),
      options: scenario.options,
      typeName: scenario.name,
      poolName: "Custom",
      order: 0,
    };
    const renderOptionExtra: OptionDisplayProps["renderOptionExtra"] = (option) => (
      <span data-option-id={option.value}>Reviewed</span>
    );
    const optionsDescription = <aside>Review labels are informational.</aside>;
    const onValueChange = vi.fn();

    await act(async () => {
      root.render(
        <PropertyValueRenderer
          isEditing
          bizValue={serializeStandardValue(scenario.bizValue, property.bizValueType)}
          dbValue={serializeStandardValue(scenario.dbValue, property.dbValueType)}
          optionsDescription={optionsDescription}
          property={property}
          renderOptionExtra={renderOptionExtra}
          onValueChange={onValueChange}
        />,
      );
    });
    const props = standardRenderer.mock.lastCall![0] as RendererProps;

    expect(props.renderOptionExtra).toBe(renderOptionExtra);
    expect(props.optionsDescription).toBe(optionsDescription);
    expect(props.editor?.value).toEqual([optionId]);
    expect(props.value).toEqual(scenario.editorBizValue);

    props.editor!.onValueChange([optionId], scenario.editorBizValue);
    const [dbValue, bizValue] = onValueChange.mock.lastCall!;

    expect(deserializeStandardValue(dbValue, property.dbValueType)).toEqual(scenario.dbValue);
    expect(deserializeStandardValue(bizValue, property.bizValueType)).toEqual(scenario.bizValue);
  });
});
