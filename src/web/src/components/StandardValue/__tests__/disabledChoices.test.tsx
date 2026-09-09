import type { ComponentType, ReactNode } from "react";
import type { DisabledChoiceKeysSource } from "@/hooks/useDisabledChoiceKeys";

import { createElement } from "react";
import { createRoot } from "react-dom/client";
import { act } from "react-dom/test-utils";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import ChoiceValueRenderer from "../ValueRenderer/Renderers/ChoiceValueRenderer";
import TagsValueRenderer from "../ValueRenderer/Renderers/TagsValueRenderer";
import MultilevelValueRenderer from "../ValueRenderer/Renderers/MultilevelValueRenderer";
import MultilevelValueEditor from "../ValueEditor/Editors/MultilevelValueEditor";

import { createDisabledChoiceKeysSource } from "@/hooks/useDisabledChoiceKeys";

const { createPortal, optionsThreshold } = vi.hoisted(() => ({
  createPortal: vi.fn(),
  optionsThreshold: { current: 30 },
}));

vi.mock("@/components/ContextProvider/BakabaseContextProvider", () => ({
  useBakabaseContext: () => ({ createPortal }),
}));
vi.mock("@/hooks/useFilterOptionsThreshold", () => ({
  DEFAULT_FILTER_OPTIONS_THRESHOLD: 30,
  getFilterOptionsThreshold: () => optionsThreshold.current,
  useFilterOptionsThreshold: () => [optionsThreshold.current],
}));
vi.mock("@/components/utils", () => ({
  buildLogger: () => () => {},
  autoBackgroundColor: (color: string) => color,
}));
vi.mock("@/components/bakaui", () => {
  const Button = ({
    children,
    isDisabled,
    onClick,
    onPress,
  }: {
    children?: ReactNode;
    isDisabled?: boolean;
    onClick?: () => void;
    onPress?: () => void;
  }) => (
    <button disabled={isDisabled} type="button" onClick={onClick ?? onPress}>
      {children}
    </button>
  );

  return {
    Button,
    Chip: Button,
    Input: ({
      placeholder,
      value,
      onValueChange,
    }: {
      placeholder?: string;
      value?: string;
      onValueChange?: (value: string) => void;
    }) => (
      <input
        placeholder={placeholder}
        value={value}
        onChange={(event) => onValueChange?.(event.target.value)}
      />
    ),
    Modal: ({ children, onOk }: { children?: ReactNode; onOk?: () => void }) => (
      <div aria-label="Select data" role="dialog">
        {children}
        <button type="button" onClick={onOk}>
          Apply
        </button>
      </div>
    ),
  };
});

type ScenarioProps = {
  selected?: string[];
  disabledKeys?: ReadonlySet<string>;
  disabledKeysSource?: DisabledChoiceKeysSource;
  onValueChange: ReturnType<typeof vi.fn>;
};

const choices = [
  { value: "unused-id", label: "Unused" },
  { value: "used-id", label: "Used" },
];
const getChoices = async () => choices;
const getTags = async () => choices.map(({ value, label }) => ({ value, name: label }));

const scenarios = [
  ...[false, true].map((multiple) => ({
    name: multiple ? "multiple choices" : "single choice",
    render: ({ selected, onValueChange, ...disabledProps }: ScenarioProps) => (
      <ChoiceValueRenderer
        isEditing
        editor={{ value: selected, onValueChange }}
        getDataSource={getChoices}
        multiple={multiple}
        {...disabledProps}
      />
    ),
  })),
  {
    name: "tags",
    render: ({ selected, onValueChange, ...disabledProps }: ScenarioProps) => (
      <TagsValueRenderer
        isEditing
        editor={{ value: selected, onValueChange }}
        getDataSource={getTags}
        {...disabledProps}
      />
    ),
  },
  ...[false, true].map((multiple) => ({
    name: multiple ? "multiple multilevel choices" : "single multilevel choice",
    render: ({ selected, onValueChange, ...disabledProps }: ScenarioProps) => (
      <MultilevelValueRenderer
        isEditing
        editor={{ value: selected, onValueChange }}
        getDataSource={getChoices}
        multiple={multiple}
        {...disabledProps}
      />
    ),
  })),
];

const mounted: { root: ReturnType<typeof createRoot>; container: HTMLDivElement }[] = [];

function mount(element: ReactNode) {
  const container = document.createElement("div");

  document.body.appendChild(container);
  const root = createRoot(container);

  mounted.push({ root, container });
  root.render(<>{element}</>);
}

async function render(element: ReactNode) {
  await act(async () => mount(element));
}

function button(name: string | RegExp, container: ParentNode = document) {
  const result = Array.from(container.querySelectorAll("button")).find((element) => {
    const label = element.textContent?.trim() ?? "";

    return typeof name === "string" ? label === name : name.test(label);
  });

  if (!result) throw new Error(`Button not found: ${name}`);

  return result;
}

async function click(element: HTMLElement) {
  await act(async () => element.click());
}

function openedDialog() {
  const dialog = document.querySelector('[role="dialog"]');

  if (!dialog) throw new Error("Expected the full editor to open");

  return dialog;
}

beforeEach(() => {
  (globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;
  optionsThreshold.current = 30;
  createPortal.mockReset();
  createPortal.mockImplementation((component: ComponentType, props: Record<string, unknown>) =>
    mount(createElement(component, props)),
  );
});

afterEach(async () => {
  for (const { root, container } of mounted.splice(0)) {
    await act(async () => root.unmount());
    container.remove();
  }
});

describe.each(scenarios)("disabled $name", ({ render: renderRenderer }) => {
  it("blocks unselected disabled choices and allows enabled choices inline", async () => {
    const onValueChange = vi.fn();

    await render(renderRenderer({ disabledKeys: new Set(["unused-id"]), onValueChange }));
    const unused = button("Unused");

    expect(unused).toBeDisabled();
    await click(unused);
    expect(onValueChange).not.toHaveBeenCalled();

    await click(button("Used"));
    expect(onValueChange.mock.calls[0][0]).toEqual(["used-id"]);
  });

  it("allows removing a selected disabled choice inline", async () => {
    const onValueChange = vi.fn();

    await render(
      renderRenderer({
        selected: ["unused-id"],
        disabledKeys: new Set(["unused-id"]),
        onValueChange,
      }),
    );
    const unused = button("Unused");

    expect(unused).toBeEnabled();
    await click(unused);
    expect(onValueChange).toHaveBeenCalledWith(undefined, undefined);
  });

  it("keeps an open full editor connected to disabled-key updates", async () => {
    optionsThreshold.current = 1;
    const source = createDisabledChoiceKeysSource();
    const onValueChange = vi.fn();

    await render(renderRenderer({ disabledKeysSource: source, onValueChange }));
    await click(button(/common.action.more/));
    const dialog = openedDialog();
    const unused = button("Unused", dialog);

    expect(unused).toBeEnabled();
    act(() => source.publish(new Set(["unused-id"])));
    expect(unused).toBeDisabled();
    await click(unused);
    await click(button("Apply", dialog));
    expect(onValueChange).toHaveBeenLastCalledWith([], []);

    act(() => source.publish(undefined));
    expect(unused).toBeEnabled();
    await click(unused);
    act(() => source.publish(new Set(["unused-id"])));
    expect(unused).toBeEnabled();

    // A choice that becomes disabled after selection remains removable.
    await click(unused);
    expect(unused).toBeDisabled();
    await click(button("Apply", dialog));
    expect(onValueChange).toHaveBeenLastCalledWith([], []);
    expect(createPortal).toHaveBeenCalledTimes(1);
  });

  it("passes static disabled keys to the full editor and permits removing existing selections", async () => {
    optionsThreshold.current = 1;
    const onValueChange = vi.fn();

    await render(
      renderRenderer({
        selected: ["unused-id"],
        disabledKeys: new Set(["unused-id", "used-id"]),
        onValueChange,
      }),
    );
    await click(button(/common.action.more/));
    const dialog = openedDialog();
    const unused = button("Unused", dialog);

    expect(button("Used", dialog)).toBeDisabled();
    expect(unused).toBeEnabled();
    await click(unused);
    expect(unused).toBeDisabled();
    await click(button("Apply", dialog));
    expect(onValueChange).toHaveBeenCalledWith([], []);
  });
});

describe("disabled multilevel navigation", () => {
  it("expands a disabled parent and selects an enabled descendant", async () => {
    const onValueChange = vi.fn();

    await render(
      <MultilevelValueEditor
        disabledKeys={new Set(["parent-id", "unused-id"])}
        getDataSource={async () => [{ value: "parent-id", label: "Parent", children: choices }]}
        onValueChange={onValueChange}
      />,
    );
    const parent = button("Parent");

    expect(parent).toBeEnabled();
    await click(parent);
    expect(button("Unused")).toBeDisabled();
    await click(button("Used"));
    await click(button("Apply"));
    expect(onValueChange).toHaveBeenCalledWith(["used-id"], [["Parent", "Used"]]);
  });

  it("matches disabled keys for numeric multilevel values", async () => {
    await render(
      <MultilevelValueEditor<number>
        disabledKeys={new Set(["42"])}
        getDataSource={async () => [{ value: 42, label: "Numeric choice" }]}
      />,
    );

    expect(button("Numeric choice")).toBeDisabled();
  });
});
