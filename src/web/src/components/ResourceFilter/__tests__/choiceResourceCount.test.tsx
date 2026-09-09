import { createRoot } from "react-dom/client";
import { act } from "react-dom/test-utils";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import ChoiceResourceCount, {
  ChoiceResourceCountsStatus,
} from "../components/Filter/ChoiceResourceCount";
import { createChoiceResourceCountsStore } from "../hooks/choiceResourceCountsStore";

vi.mock("react-i18next", () => ({ useTranslation: () => ({ t: (key: string) => key }) }));

let container: HTMLDivElement;
let root: ReturnType<typeof createRoot>;

beforeEach(() => {
  (globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;
  container = document.createElement("div");
  document.body.appendChild(container);
  root = createRoot(container);
});

afterEach(async () => {
  await act(async () => root.unmount());
  container.remove();
});

describe("filter choice resource count", () => {
  it("keeps a mounted badge's slot width across digit changes, zero, and unavailable counts", async () => {
    const store = createChoiceResourceCountsStore();

    store.publish({ counts: { choice: 1_000 }, reservedWidths: { choice: 7 } });
    await act(async () => root.render(<ChoiceResourceCount choiceId="choice" store={store} />));
    const badge = container.querySelector("span")!;

    for (const count of [9, 10, 1_000, 0, undefined]) {
      act(() =>
        store.publish({
          counts: count === undefined ? undefined : { choice: count },
          reservedWidths: { choice: 7 },
        }),
      );
      expect(container.querySelector("span")).toBe(badge);
      expect(badge.style.width).toBe("7ch");
      expect(badge.style.flexShrink).toBe("0");
      expect(badge.style.whiteSpace).toBe("nowrap");
      expect(badge.textContent).toBe(count ? `(${count.toLocaleString()})` : "");
      expect(badge.getAttribute("aria-hidden")).toBe(String(!count));
    }
  });

  it("updates an already mounted badge when only the status changes", async () => {
    const store = createChoiceResourceCountsStore();
    const counts = { choice: 9 };

    store.publish({ counts, reservedWidths: { choice: 7 } });
    await act(async () => root.render(<ChoiceResourceCount choiceId="choice" store={store} />));
    const badge = container.querySelector("span")!;

    expect(badge.className).toContain("opacity-70");
    act(() => store.publish({ counts, reservedWidths: { choice: 7 }, loading: true, stale: true }));
    expect(badge.textContent).toBe("(9)");
    expect(badge.className).toContain("opacity-40");
    expect(badge.title).toBe("property.reference.updatingCounts");
    expect(badge.style.width).toBe("7ch");

    act(() => store.publish({ counts, reservedWidths: { choice: 7 }, error: true, stale: true }));
    expect(badge.textContent).toBe("(9)");
    expect(badge.title).toBe("property.reference.countsUpdateFailed");
    expect(badge.className).toContain("opacity-40");
  });

  it("does not introduce placeholder badges without an assigned slot", async () => {
    const store = createChoiceResourceCountsStore();

    store.publish({ counts: { zero: 0 }, reservedWidths: { anotherChoice: 4 }, loading: true });
    await act(async () =>
      root.render(
        <>
          <ChoiceResourceCount choiceId="zero" store={store} />
          <ChoiceResourceCount choiceId="missing" store={store} />
        </>,
      ),
    );
    expect(container.innerHTML).toBe("");
  });

  it("hides zero and missing counts while displaying positive counts", async () => {
    const store = createChoiceResourceCountsStore();

    store.publish({ counts: { zero: 0, positive: 3 } });
    await act(async () =>
      root.render(
        <>
          <ChoiceResourceCount choiceId="zero" store={store} />
          <ChoiceResourceCount choiceId="missing" store={store} />
          <ChoiceResourceCount choiceId="positive" store={store} />
        </>,
      ),
    );
    expect(container.textContent).toBe("(3)");
    expect(container.querySelectorAll("span")).toHaveLength(1);
  });

  it("reserves one status line and clears its accessible text when the request settles", async () => {
    const store = createChoiceResourceCountsStore();

    store.publish({ loading: true });
    await act(async () => root.render(<ChoiceResourceCountsStatus store={store} />));
    const status = container.querySelector('[role="status"]')!;
    const className = status.className;

    expect(status.textContent).toBe("property.reference.loadingCounts");
    expect(status.getAttribute("aria-hidden")).toBe("false");
    expect(status.className).toContain("relative");
    expect(status.firstElementChild?.className).toBe("absolute inset-x-0 top-0 truncate");
    act(() => store.publish({ counts: { choice: 9 } }));
    expect(container.querySelector('[role="status"]')).toBe(status);
    expect(status.className).toBe(className);
    expect(status.className).toContain("h-4");
    expect(status.textContent).toBe("");
    expect(status.getAttribute("aria-hidden")).toBe("true");

    act(() => store.publish({ counts: { choice: 9 }, stale: true, loading: true }));
    expect(status.textContent).toBe("property.reference.updatingCounts");
    expect(status.className).toBe(className);
  });

  it("distinguishes failed initial loading from failed updates with retained results", async () => {
    const store = createChoiceResourceCountsStore();

    store.publish({ error: true });
    await act(async () => root.render(<ChoiceResourceCountsStatus store={store} />));
    expect(container.textContent).toBe("property.reference.countsLoadFailed");
    act(() => store.publish({ counts: { choice: 9 }, error: true, stale: true }));
    expect(container.textContent).toBe("property.reference.countsUpdateFailed");
  });

  it("publishes counts and their status as one snapshot", () => {
    const store = createChoiceResourceCountsStore();
    const retained = { counts: { previous: 5 }, loading: true, stale: true };

    store.publish(retained);
    const observed: unknown[] = [];
    const unsubscribe = store.subscribe(() => observed.push(store.getSnapshot()));
    const updated = {
      counts: { next: 10 },
      reservedWidths: { next: 4 },
      loading: false,
      stale: false,
    };

    store.publish(updated);
    expect(observed).toEqual([updated]);
    expect(store.getSnapshot()).toBe(updated);
    unsubscribe();
    store.publish(retained);
    expect(observed).toEqual([updated]);
  });
});
