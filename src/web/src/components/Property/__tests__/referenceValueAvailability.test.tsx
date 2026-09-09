import type { Props } from "@/components/Property/components/PropertyValueRenderer";

import { act } from "react-dom/test-utils";
import { createRoot } from "react-dom/client";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import ReferenceValueCount from "../components/ReferenceValueCount";

import ReferencePropertyValueInput from "@/components/ResourceFilter/components/Filter/ReferencePropertyValueInput";
import ReferenceValueUsage, {
  ReferenceValueUsageProvider,
} from "@/components/PropertyModal/components/ReferenceValueUsage";
import { PropertyPool, PropertyType, StandardValueType } from "@/sdk/constants";

const state = vi.hoisted(() => ({
  globalCounts: undefined as Record<string, number> | undefined,
  filteredCounts: undefined as Record<string, number> | undefined,
  search: undefined as { keyword: string } | undefined,
  rendererProps: undefined as Props | undefined,
  createPortal: vi.fn(),
}));

vi.mock("react-i18next", () => ({ useTranslation: () => ({ t: (key: string) => key }) }));
vi.mock("@/components/Property/components/PropertyValueRenderer", () => ({
  default: (props: Props) => {
    state.rendererProps = props;

    return null;
  },
}));
vi.mock("@/hooks/useReferenceValueResourceCounts", () => ({
  useReferenceValueSearch: () => state.search,
  referenceValueCountsCriteria: (search?: { keyword: string }) => search ?? {},
  useReferenceValueResourceCounts: (property?: unknown, search?: unknown) => ({
    counts: property ? (search ? state.filteredCounts : state.globalCounts) : undefined,
    source: undefined,
    loading: false,
    error: false,
    refresh: vi.fn(),
  }),
}));
vi.mock("@/components/ContextProvider/BakabaseContextProvider", () => ({
  useBakabaseContext: () => ({ createPortal: state.createPortal }),
}));
vi.mock("@/components/PropertyModal/components/ReferenceValueResourcesModal", () => ({
  default: () => null,
}));
vi.mock("@/components/bakaui", () => ({
  Button: ({ children, isDisabled, onPress, title, "aria-label": ariaLabel }: any) => (
    <button aria-label={ariaLabel} disabled={isDisabled} title={title} onClick={onPress}>
      {children}
    </button>
  ),
}));

const property: Props["property"] = {
  id: 1,
  pool: PropertyPool.Custom,
  type: PropertyType.SingleChoice,
  name: "Genre",
  dbValueType: StandardValueType.String,
  bizValueType: StandardValueType.String,
  typeName: "SingleChoice",
  poolName: "Custom",
  order: 0,
};
let container: HTMLDivElement;
let root: ReturnType<typeof createRoot>;

beforeEach(() => {
  (globalThis as any).IS_REACT_ACT_ENVIRONMENT = true;
  state.globalCounts = undefined;
  state.filteredCounts = undefined;
  state.search = undefined;
  state.rendererProps = undefined;
  state.createPortal.mockClear();
  container = document.createElement("div");
  document.body.appendChild(container);
  root = createRoot(container);
});
afterEach(async () => {
  await act(async () => root.unmount());
  container.remove();
});

describe("reference option availability", () => {
  it("keeps a valid replacement enabled when its current-filter count is zero", async () => {
    state.search = { keyword: "current filter" };
    state.globalCounts = { selected: 2, replacement: 7, unused: 0 };
    state.filteredCounts = { selected: 2, replacement: 0, unused: 0 };
    await act(async () =>
      root.render(<ReferencePropertyValueInput dbValue="selected" property={property} />),
    );

    expect(state.rendererProps?.resourceCounts).toEqual(state.filteredCounts);
    expect([...state.rendererProps!.disabledKeys!]).toEqual(["unused"]);
    expect(state.rendererProps!.disabledKeysSource!.getSnapshot()?.has("replacement")).toBe(false);
  });

  it("does not turn missing or unavailable counts into disabled choices", async () => {
    state.search = { keyword: "current filter" };
    state.filteredCounts = { choice: 0 };
    await act(async () => root.render(<ReferencePropertyValueInput property={property} />));
    expect(state.rendererProps?.disabledKeys).toBeUndefined();

    state.globalCounts = { known: 3 };
    await act(async () => root.render(<ReferencePropertyValueInput property={property} />));
    expect(state.rendererProps?.disabledKeys?.has("choice")).toBe(false);
  });

  it("keeps the displayed count source connected when switching between global and filtered counts", async () => {
    state.globalCounts = { choice: 7 };
    await act(async () => root.render(<ReferencePropertyValueInput property={property} />));
    const source = state.rendererProps!.resourceCountsSource!;

    expect(source.getSnapshot()).toEqual({ choice: 7 });

    state.search = { keyword: "limited" };
    state.filteredCounts = { choice: 0 };
    await act(async () => root.render(<ReferencePropertyValueInput property={property} />));
    expect(state.rendererProps!.resourceCountsSource).toBe(source);
    expect(source.getSnapshot()).toEqual({ choice: 0 });

    state.search = undefined;
    await act(async () => root.render(<ReferencePropertyValueInput property={property} />));
    expect(state.rendererProps!.resourceCountsSource).toBe(source);
    expect(source.getSnapshot()).toEqual({ choice: 7 });
  });

  it("publishes changes to the existing disabled source and retains explicit restrictions", async () => {
    const disabledKeys = new Set(["explicit"]);

    state.globalCounts = { unused: 0 };
    await act(async () =>
      root.render(<ReferencePropertyValueInput disabledKeys={disabledKeys} property={property} />),
    );
    const source = state.rendererProps!.disabledKeysSource!;
    const listener = vi.fn();
    const unsubscribe = source.subscribe(listener);

    expect(source.getSnapshot()).toEqual(new Set(["unused", "explicit"]));

    state.globalCounts = { unused: 4 };
    await act(async () =>
      root.render(<ReferencePropertyValueInput disabledKeys={disabledKeys} property={property} />),
    );
    expect(state.rendererProps!.disabledKeysSource).toBe(source);
    expect(source.getSnapshot()).toEqual(new Set(["explicit"]));
    expect(listener).toHaveBeenCalledTimes(1);
    unsubscribe();
  });

  it("hides zero badges while keeping positive badges", async () => {
    await act(async () =>
      root.render(
        <>
          <ReferenceValueCount count={0} />
          <ReferenceValueCount />
          <ReferenceValueCount count={3} />
        </>,
      ),
    );
    expect(container.textContent).toBe("(3)");
    expect(container.querySelectorAll("span")).toHaveLength(1);
  });

  it("hides zero in settings and disables its empty resource search without affecting used values", async () => {
    state.globalCounts = { unused: 0, used: 5 };
    await act(async () =>
      root.render(
        <ReferenceValueUsageProvider
          options={{ choices: [{ value: "unused" }, { value: "used" }] }}
          property={property}
        >
          <ReferenceValueUsage value="unused" />
          <ReferenceValueUsage value="used" />
        </ReferenceValueUsageProvider>,
      ),
    );
    const buttons = [...container.querySelectorAll("button")];

    expect(buttons[0].disabled).toBe(true);
    expect(buttons[1].disabled).toBe(false);
    expect(container.textContent).not.toContain("0");
    expect(container.textContent).toContain("5");
    await act(async () => {
      buttons[0].click();
    });
    expect(state.createPortal).not.toHaveBeenCalled();
  });

  it("keeps saved values searchable while counts load, but disallows draft option searches", async () => {
    await act(async () =>
      root.render(
        <ReferenceValueUsageProvider
          options={{ choices: [{ value: "saved" }] }}
          property={property}
        >
          <ReferenceValueUsage value="saved" />
          <ReferenceValueUsage value="draft" />
        </ReferenceValueUsageProvider>,
      ),
    );
    const buttons = [...container.querySelectorAll("button")];

    expect(buttons[0].disabled).toBe(false);
    expect(buttons[1].disabled).toBe(true);
  });
});
