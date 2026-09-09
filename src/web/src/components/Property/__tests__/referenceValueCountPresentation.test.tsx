import { createRoot } from "react-dom/client";
import { act } from "react-dom/test-utils";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import ReferenceValueCount, { ReferenceValueCountsStatus } from "../components/ReferenceValueCount";

import { createResourceCountsSource } from "@/hooks/useResourceCountsSource";

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

describe("reference count presentation", () => {
  it("keeps the same slot width across digit changes, zero, and unavailable counts", async () => {
    const source = createResourceCountsSource();

    source.publish({ choice: 1000 }, { reservedWidths: { choice: 7 } });
    for (const count of [9, 10, 1000, 0, undefined]) {
      await act(async () =>
        root.render(<ReferenceValueCount count={count} source={source} valueId="choice" />),
      );
      const badge = container.querySelector("span")!;

      expect(badge.style.width).toBe("7ch");
      expect(badge.style.flexShrink).toBe("0");
      expect(badge.style.whiteSpace).toBe("nowrap");
      expect(badge.textContent).toBe(count ? `(${count.toLocaleString()})` : "");
      expect(badge.getAttribute("aria-hidden")).toBe(String(!count));
    }
  });

  it("updates an already mounted badge when only the presentation changes", async () => {
    const source = createResourceCountsSource();
    const counts = { choice: 9 };

    source.publish(counts, { reservedWidths: { choice: 7 } });
    await act(async () =>
      root.render(<ReferenceValueCount count={9} source={source} valueId="choice" />),
    );
    const badge = container.querySelector("span")!;

    expect(badge.className).toContain("opacity-70");
    act(() =>
      source.publish(counts, { reservedWidths: { choice: 7 }, loading: true, stale: true }),
    );
    expect(badge.textContent).toBe("(9)");
    expect(badge.className).toContain("opacity-40");
    expect(badge.title).toBe("property.reference.updatingCounts");
    expect(badge.style.width).toBe("7ch");

    act(() => source.publish(counts, { reservedWidths: { choice: 7 }, error: true, stale: true }));
    expect(badge.textContent).toBe("(9)");
    expect(badge.title).toBe("property.reference.countsUpdateFailed");
    expect(badge.className).toContain("opacity-40");
  });

  it("does not introduce placeholder badges without an assigned slot", async () => {
    const source = createResourceCountsSource();

    source.publish(undefined, { reservedWidths: { anotherChoice: 4 }, loading: true });
    await act(async () =>
      root.render(
        <>
          <ReferenceValueCount count={0} />
          <ReferenceValueCount />
          <ReferenceValueCount count={0} source={source} valueId="choice" />
        </>,
      ),
    );
    expect(container.innerHTML).toBe("");
  });

  it("reserves one status line and clears its accessible text when the request settles", async () => {
    const source = createResourceCountsSource();

    source.publish(undefined, { loading: true });
    await act(async () => root.render(<ReferenceValueCountsStatus source={source} />));
    const status = container.querySelector('[role="status"]')!;
    const className = status.className;

    expect(status.textContent).toBe("property.reference.loadingCounts");
    expect(status.getAttribute("aria-hidden")).toBe("false");
    expect(status.className).toContain("relative");
    expect(status.firstElementChild?.className).toBe("absolute inset-x-0 top-0 truncate");
    act(() => source.publish({ choice: 9 }, {}));
    expect(container.querySelector('[role="status"]')).toBe(status);
    expect(status.className).toBe(className);
    expect(status.className).toContain("h-4");
    expect(status.textContent).toBe("");
    expect(status.getAttribute("aria-hidden")).toBe("true");

    act(() => source.publish({ choice: 9 }, { stale: true, loading: true }));
    expect(status.textContent).toBe("property.reference.updatingCounts");
    expect(status.className).toBe(className);
  });

  it("distinguishes failed initial loading from failed updates with retained results", async () => {
    const source = createResourceCountsSource();

    source.publish(undefined, { error: true });
    await act(async () => root.render(<ReferenceValueCountsStatus source={source} />));
    expect(container.textContent).toBe("property.reference.countsLoadFailed");
    act(() => source.publish({ choice: 9 }, { error: true, stale: true }));
    expect(container.textContent).toBe("property.reference.countsUpdateFailed");
  });

  it("omits the status line in ordinary editors without count presentation", async () => {
    const source = createResourceCountsSource();

    source.publish({ choice: 9 });
    await act(async () => root.render(<ReferenceValueCountsStatus source={source} />));
    expect(container.innerHTML).toBe("");
  });
});
