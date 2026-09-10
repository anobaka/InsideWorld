import type { Props } from "@/components/Property/components/PropertyValueRenderer";

import { createRoot } from "react-dom/client";
import { act } from "react-dom/test-utils";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import ReferenceValueUsage, {
  ReferenceValueUsageProvider,
} from "../components/ReferenceValueUsage";

import { PropertyPool, PropertyType, StandardValueType } from "@/sdk/constants";

const state = vi.hoisted(() => ({
  counts: undefined as Record<string, number> | undefined,
  createPortal: vi.fn(),
}));

vi.mock("react-i18next", () => ({ useTranslation: () => ({ t: (key: string) => key }) }));
vi.mock("@/hooks/useReferenceValueResourceCounts", () => ({
  useReferenceValueResourceCounts: () => ({
    counts: state.counts,
    loading: state.counts === undefined,
    error: false,
    stale: false,
    refresh: vi.fn(),
  }),
}));
vi.mock("@/components/ContextProvider/BakabaseContextProvider", () => ({
  useBakabaseContext: () => ({ createPortal: state.createPortal }),
}));
vi.mock("../components/ReferenceValueResourcesModal", () => ({ default: () => null }));
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
  (globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;
  state.counts = undefined;
  state.createPortal.mockClear();
  container = document.createElement("div");
  document.body.appendChild(container);
  root = createRoot(container);
});

afterEach(async () => {
  await act(async () => root.unmount());
  container.remove();
});

describe("reference value usage in property settings", () => {
  it("hides zero counts and disables empty resource searches without affecting used values", async () => {
    state.counts = { unused: 0, used: 5 };
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
    await act(async () => buttons[0].click());
    expect(state.createPortal).not.toHaveBeenCalled();
  });

  it("keeps saved values searchable while counts load and disallows draft searches", async () => {
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
