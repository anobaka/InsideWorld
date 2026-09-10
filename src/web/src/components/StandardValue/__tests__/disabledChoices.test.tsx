import type { ComponentType, ReactNode } from "react";
import type { DisabledChoiceKeysSource } from "@/hooks/useDisabledChoiceKeys";
import type { OptionDisplayProps } from "../OptionDisplayProps";

import { createElement, useSyncExternalStore } from "react";
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

type ScenarioProps = OptionDisplayProps & {
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
    expectedBizValue: ["Unused"],
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
    expectedBizValue: [{ name: "Unused", group: undefined }],
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
    expectedBizValue: [["Unused"]],
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

async function search(dialog: ParentNode, keyword: string) {
  const input = dialog.querySelector("input")!;
  const setValue = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, "value")!.set!;

  await act(async () => {
    setValue.call(input, keyword);
    input.dispatchEvent(new Event("input", { bubbles: true }));
  });
}

function createReviewSlots() {
  let snapshot = { status: "Pending review", description: "Reviews in progress" };
  const listeners = new Set<() => void>();
  const subscribe = (listener: () => void) => {
    listeners.add(listener);

    return () => listeners.delete(listener);
  };
  const getSnapshot = () => snapshot;
  const ReviewBadge = ({ value, label }: { value: string; label: string }) => {
    const review = useSyncExternalStore(subscribe, getSnapshot);

    return (
      <span data-label={label} data-option-extra={value}>
        {review.status}
      </span>
    );
  };
  const ReviewDescription = () => {
    const review = useSyncExternalStore(subscribe, getSnapshot);

    return <div role="status">{review.description}</div>;
  };

  return {
    renderOptionExtra: (option: { value: string; label: string }) => <ReviewBadge {...option} />,
    optionsDescription: <ReviewDescription />,
    publish: (next: typeof snapshot) => {
      snapshot = next;
      listeners.forEach((listener) => listener());
    },
  };
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

describe.each(scenarios)(
  "option display slots for $name",
  ({ render: renderRenderer, expectedBizValue }) => {
    it("appends caller content inline while retaining the existing option value mapping", async () => {
      const onValueChange = vi.fn();
      const slots = createReviewSlots();

      await render(renderRenderer({ ...slots, onValueChange }));
      const choice = button("UnusedPending review");
      const badge = choice.querySelector('[data-option-extra="unused-id"]')!;

      expect(badge.getAttribute("data-label")).toBe("Unused");
      expect(choice.textContent).toBe("UnusedPending review");
      await click(choice);
      expect(onValueChange.mock.lastCall?.[0]).toEqual(["unused-id"]);
      expect(JSON.stringify(onValueChange.mock.lastCall?.[1])).not.toContain("Pending review");
    });

    it("keeps caller-owned metadata connected in an open full editor without changing selection", async () => {
      optionsThreshold.current = 1;
      const slots = createReviewSlots();
      const onValueChange = vi.fn();

      await render(renderRenderer({ ...slots, onValueChange }));
      await click(button(/common.action.more/));
      const dialog = openedDialog();
      const choice = button("UnusedPending review", dialog);
      const badge = choice.querySelector('[data-option-extra="unused-id"]')!;

      expect(badge.getAttribute("data-label")).toBe("Unused");
      expect(dialog.querySelector('[role="status"]')?.textContent).toBe("Reviews in progress");
      await click(choice);
      act(() => slots.publish({ status: "Approved", description: "Reviews complete" }));

      expect(choice.querySelector('[data-option-extra="unused-id"]')).toBe(badge);
      expect(badge.textContent).toBe("Approved");
      expect(dialog.querySelector('[role="status"]')?.textContent).toBe("Reviews complete");
      expect(choice).toBeEnabled();
      expect(onValueChange).not.toHaveBeenCalled();
      await click(button("Apply", dialog));
      expect(onValueChange).toHaveBeenCalledWith(["unused-id"], expectedBizValue);
      expect(createPortal).toHaveBeenCalledTimes(1);
    });

    it("searches the original label without matching appended metadata", async () => {
      optionsThreshold.current = 1;
      const slots = createReviewSlots();
      const onValueChange = vi.fn();

      await render(renderRenderer({ ...slots, onValueChange }));
      await click(button(/common.action.more/));
      const dialog = openedDialog();

      await search(dialog, "Pending review");
      expect(dialog.querySelectorAll("[data-option-extra]")).toHaveLength(0);
      await search(dialog, "unused");
      expect(dialog.querySelectorAll("[data-option-extra]")).toHaveLength(1);
      await click(button("UnusedPending review", dialog));
      await click(button("Apply", dialog));
      expect(onValueChange).toHaveBeenCalledWith(["unused-id"], expectedBizValue);
    });

    it("renders ordinary labels without extra markup or status when no slots are supplied", async () => {
      optionsThreshold.current = 1;

      await render(renderRenderer({ onValueChange: vi.fn() }));
      expect(button("Unused").textContent).toBe("Unused");
      expect(document.querySelector("[data-option-extra]")).toBeNull();
      expect(document.querySelector('[role="status"]')).toBeNull();
      await click(button(/common.action.more/));
      const dialog = openedDialog();

      expect(button("Unused", dialog).textContent).toBe("Unused");
      expect(button("Used", dialog).textContent).toBe("Used");
      expect(dialog.querySelector("[data-option-extra]")).toBeNull();
      expect(dialog.querySelector('[role="status"]')).toBeNull();
    });
  },
);

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
