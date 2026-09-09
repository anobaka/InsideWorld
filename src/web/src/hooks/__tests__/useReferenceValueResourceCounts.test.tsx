import type { ReferenceProperty } from "@/components/Property/referenceValues";
import type { SearchForm } from "@/pages/resource/models";

import { act } from "react-dom/test-utils";
import { createRoot } from "react-dom/client";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import {
  referenceValueCountsCriteria,
  useReferenceValueResourceCounts,
} from "../useReferenceValueResourceCounts";
import { createResourceCountsSource, useResourceCountsSource } from "../useResourceCountsSource";

import { InternalProperty, PropertyPool, PropertyType, SearchOperation } from "@/sdk/constants";
import { resourceChangedChannel } from "@/services/ResourceChangedChannel";

const { request } = vi.hoisted(() => ({ request: vi.fn() }));

vi.mock("@/sdk/BApi", () => ({ default: { request } }));

const property = { id: 87, pool: PropertyPool.Custom, type: PropertyType.Tags, name: "Tags" };
let latest: ReturnType<typeof useReferenceValueResourceCounts>;
let root: ReturnType<typeof createRoot>;
let container: HTMLDivElement;

function Probe({
  search,
  reference = property,
}: {
  search?: SearchForm;
  reference?: ReferenceProperty;
}) {
  latest = useReferenceValueResourceCounts(reference, search);

  return null;
}

beforeEach(() => {
  (globalThis as any).IS_REACT_ACT_ENVIRONMENT = true;
  vi.useFakeTimers();
  request.mockReset();
  container = document.createElement("div");
  document.body.appendChild(container);
  root = createRoot(container);
});
afterEach(async () => {
  await act(async () => root.unmount());
  container.remove();
  vi.useRealTimers();
});

describe("reference resource counts", () => {
  it("keeps full filter criteria while excluding pagination and ordering", () => {
    const search = {
      page: 6,
      pageSize: 50,
      keyword: "folder",
      tags: [1],
      orders: [{ property: 1, asc: true }],
      group: {
        combinator: 2,
        disabled: false,
        groups: [{ combinator: 1, disabled: true }],
        filters: [
          {
            propertyId: 3,
            propertyPool: PropertyPool.Custom,
            operation: SearchOperation.Contains,
            dbValue: "ID",
            disabled: false,
            bizValue: "must not go over the wire",
          },
        ],
      },
    };
    const criteria = referenceValueCountsCriteria(search);

    expect(criteria).toEqual({
      keyword: "folder",
      tags: [1],
      group: {
        combinator: 2,
        disabled: false,
        groups: [{ combinator: 1, disabled: true }],
        filters: [
          {
            propertyId: 3,
            propertyPool: PropertyPool.Custom,
            operation: SearchOperation.Contains,
            dbValue: "ID",
            disabled: false,
          },
        ],
      },
    });
    expect(referenceValueCountsCriteria({ ...search, page: 1, pageSize: 100 })).toEqual(criteria);
  });

  it("debounces edits, shares requests, and ignores stale responses", async () => {
    let resolveFirst: (value: unknown) => void = () => {};

    request.mockImplementationOnce(
      () =>
        new Promise((resolve) => {
          resolveFirst = resolve;
        }),
    );
    request.mockResolvedValue({ data: { isReady: true, counts: { choice: 2 } } });
    const initial = { page: 1, pageSize: 50, keyword: "first" };

    await act(async () =>
      root.render(
        <>
          <Probe search={initial} />
          <Probe search={initial} />
        </>,
      ),
    );
    await act(async () => {
      await vi.advanceTimersByTimeAsync(999);
    });
    expect(request).not.toHaveBeenCalled();
    await act(async () => {
      await vi.advanceTimersByTimeAsync(1);
    });
    expect(request).toHaveBeenCalledTimes(1);

    await act(async () => root.render(<Probe search={{ ...initial, keyword: "latest" }} />));
    await act(async () => {
      await vi.advanceTimersByTimeAsync(1000);
    });
    expect(latest.counts).toEqual({ choice: 2 });
    await act(async () => {
      resolveFirst({ data: { isReady: true, counts: { choice: 99 } } });
    });
    expect(latest.counts).toEqual({ choice: 2 });
  });

  it("does not report zero while the index warms, and retries", async () => {
    request.mockResolvedValueOnce({ data: { isReady: false, counts: { choice: 0 } } });
    request.mockResolvedValueOnce({ data: { isReady: true, counts: { choice: 4, unused: 0 } } });
    await act(async () => root.render(<Probe />));
    await act(async () => {
      await vi.advanceTimersByTimeAsync(0);
    });
    expect(latest.loading).toBe(true);
    expect(latest.counts).toBeUndefined();
    await act(async () => {
      await vi.advanceTimersByTimeAsync(2000);
    });
    expect(latest.counts).toEqual({ choice: 4, unused: 0 });
    expect(latest.loading).toBe(false);
  });

  it("skips unsupported dynamic parent and legacy library properties", async () => {
    await act(async () =>
      root.render(
        <Probe
          reference={{
            ...property,
            pool: PropertyPool.Internal,
            id: InternalProperty.ParentResource,
          }}
        />,
      ),
    );
    await act(async () => {
      await vi.advanceTimersByTimeAsync(2000);
    });
    expect(request).not.toHaveBeenCalled();
    await act(async () =>
      root.render(
        <Probe
          reference={{
            ...property,
            pool: PropertyPool.Internal,
            id: InternalProperty.MediaLibraryV2,
          }}
        />,
      ),
    );
    await act(async () => {
      await vi.advanceTimersByTimeAsync(2000);
    });
    expect(request).not.toHaveBeenCalled();
  });

  it("refreshes counts after resource changes while coalescing notification bursts", async () => {
    request.mockResolvedValueOnce({ data: { isReady: true, counts: { choice: 4 } } });
    request.mockResolvedValueOnce({ data: { isReady: true, counts: { choice: 3 } } });
    await act(async () => root.render(<Probe />));
    await act(async () => {
      await vi.advanceTimersByTimeAsync(0);
    });
    expect(latest.counts).toEqual({ choice: 4 });
    await act(async () => {
      resourceChangedChannel.publish([1]);
      resourceChangedChannel.publish([2]);
      await vi.advanceTimersByTimeAsync(999);
    });
    expect(request).toHaveBeenCalledTimes(1);
    await act(async () => {
      await vi.advanceTimersByTimeAsync(2);
    });
    await act(async () => {
      await vi.advanceTimersByTimeAsync(0);
    });
    expect(request).toHaveBeenCalledTimes(2);
    expect(latest.counts).toEqual({ choice: 3 });
  });

  it("updates an already-mounted selector source when counts arrive or filters change", async () => {
    const source = createResourceCountsSource();

    function Selector() {
      const counts = useResourceCountsSource(source);

      return <span>{counts?.choice ?? "pending"}</span>;
    }
    await act(async () => root.render(<Selector />));
    expect(container.textContent).toBe("pending");
    await act(async () => source.publish({ choice: 8 }));
    expect(container.textContent).toBe("8");
    await act(async () => source.publish(undefined));
    expect(container.textContent).toBe("pending");
    await act(async () => source.publish({ choice: 0 }));
    expect(container.textContent).toBe("0");
  });
});
