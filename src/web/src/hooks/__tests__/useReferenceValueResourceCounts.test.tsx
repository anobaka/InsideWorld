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

  it("retains the last counts and mounted source throughout filter debounce and loading", async () => {
    let resolveNext: (value: unknown) => void = () => {};

    request.mockResolvedValueOnce({ data: { isReady: true, counts: { choice: 12, unused: 0 } } });
    request.mockImplementationOnce(
      () =>
        new Promise((resolve) => {
          resolveNext = resolve;
        }),
    );
    await act(async () => root.render(<Probe />));
    await act(async () => {
      await vi.advanceTimersByTimeAsync(0);
    });
    const source = latest.source;
    const snapshots: unknown[] = [];
    const unsubscribe = source.subscribe(() => snapshots.push(source.getSnapshot()));

    await act(async () =>
      root.render(<Probe search={{ page: 1, pageSize: 50, keyword: "next" }} />),
    );
    expect(latest).toMatchObject({
      counts: { choice: 12, unused: 0 },
      loading: true,
      error: false,
      stale: true,
    });
    expect(latest.source).toBe(source);
    expect(source.getSnapshot()).toEqual({ choice: 12, unused: 0 });
    await act(async () => {
      await vi.advanceTimersByTimeAsync(999);
    });
    expect(request).toHaveBeenCalledTimes(1);
    await act(async () => {
      await vi.advanceTimersByTimeAsync(1);
    });
    expect(latest.loading).toBe(true);
    expect(snapshots).toEqual([]);
    await act(async () =>
      resolveNext({ data: { isReady: true, counts: { choice: 2, unused: 0 } } }),
    );
    expect(latest).toMatchObject({
      counts: { choice: 2, unused: 0 },
      loading: false,
      error: false,
      stale: false,
    });
    expect(snapshots).toEqual([{ choice: 2, unused: 0 }]);
    unsubscribe();
  });

  it("starts a new loading cycle when reverting to previously successful criteria", async () => {
    request.mockResolvedValueOnce({ data: { isReady: true, counts: { choice: 4 } } });
    request.mockResolvedValueOnce({ data: { isReady: true, counts: { choice: 5 } } });
    await act(async () =>
      root.render(<Probe search={{ page: 1, pageSize: 50, keyword: "first" }} />),
    );
    await act(async () => {
      await vi.advanceTimersByTimeAsync(1000);
    });
    await act(async () =>
      root.render(<Probe search={{ page: 1, pageSize: 50, keyword: "next" }} />),
    );
    await act(async () => {
      await vi.advanceTimersByTimeAsync(500);
    });
    await act(async () =>
      root.render(<Probe search={{ page: 1, pageSize: 50, keyword: "first" }} />),
    );
    expect(latest).toMatchObject({ counts: { choice: 4 }, loading: true, stale: true });
    await act(async () => {
      await vi.advanceTimersByTimeAsync(999);
    });
    expect(request).toHaveBeenCalledTimes(1);
    await act(async () => {
      await vi.advanceTimersByTimeAsync(1);
    });
    expect(latest).toMatchObject({ counts: { choice: 5 }, loading: false, stale: false });
    expect(request).toHaveBeenCalledTimes(2);
  });

  it("preserves successful counts through manual refresh, warming, failure, and recovery", async () => {
    request.mockResolvedValueOnce({ data: { isReady: true, counts: { choice: 8, unused: 0 } } });
    request.mockResolvedValueOnce({ data: { isReady: false, counts: { choice: 0, unused: 0 } } });
    request.mockRejectedValueOnce(new Error("offline"));
    request.mockResolvedValueOnce({ data: { isReady: true, counts: { choice: 6, unused: 0 } } });
    await act(async () => root.render(<Probe />));
    await act(async () => {
      await vi.advanceTimersByTimeAsync(0);
    });
    await act(async () => latest.refresh());
    expect(latest).toMatchObject({
      counts: { choice: 8, unused: 0 },
      loading: true,
      error: false,
      stale: true,
    });
    await act(async () => {
      await vi.advanceTimersByTimeAsync(0);
    });
    expect(latest.counts).toEqual({ choice: 8, unused: 0 });
    expect(latest.loading).toBe(true);
    await act(async () => {
      await vi.advanceTimersByTimeAsync(2000);
    });
    expect(latest).toMatchObject({
      counts: { choice: 8, unused: 0 },
      loading: false,
      error: true,
      stale: true,
    });
    expect(latest.source.getSnapshot()).toEqual({ choice: 8, unused: 0 });
    await act(async () => latest.refresh());
    expect(latest).toMatchObject({ loading: true, error: false, stale: true });
    await act(async () => {
      await vi.advanceTimersByTimeAsync(0);
    });
    expect(latest).toMatchObject({
      counts: { choice: 6, unused: 0 },
      loading: false,
      error: false,
      stale: false,
    });
  });

  it.each([
    ["ID", { ...property, id: property.id + 1 }],
    ["pool", { ...property, pool: PropertyPool.Internal }],
    ["type", { ...property, type: PropertyType.MultipleChoice }],
  ])("does not share retained counts across property %s changes", async (_name, nextProperty) => {
    request.mockResolvedValueOnce({ data: { isReady: true, counts: { choice: 8 } } });
    request.mockResolvedValueOnce({ data: { isReady: true, counts: { other: 3 } } });
    request.mockResolvedValueOnce({ data: { isReady: true, counts: { choice: 7 } } });
    await act(async () => root.render(<Probe />));
    await act(async () => {
      await vi.advanceTimersByTimeAsync(0);
    });
    await act(async () => root.render(<Probe reference={nextProperty} />));
    expect(latest.counts).toBeUndefined();
    expect(latest.source.getSnapshot()).toBeUndefined();
    expect(latest.loading).toBe(true);
    await act(async () => {
      await vi.advanceTimersByTimeAsync(0);
    });
    expect(latest.counts).toEqual({ other: 3 });
    await act(async () => root.render(<Probe />));
    expect(latest.counts).toBeUndefined();
    expect(latest.source.getSnapshot()).toBeUndefined();
    await act(async () => {
      await vi.advanceTimersByTimeAsync(0);
    });
    expect(latest.counts).toEqual({ choice: 7 });
  });

  it("clears counts and cancels notification debounce while disabled", async () => {
    request.mockResolvedValueOnce({ data: { isReady: true, counts: { choice: 0 } } });
    request.mockResolvedValueOnce({ data: { isReady: true, counts: { choice: 1 } } });
    await act(async () => root.render(<Probe />));
    await act(async () => {
      await vi.advanceTimersByTimeAsync(0);
    });
    await act(async () => resourceChangedChannel.publish([1]));
    expect(latest).toMatchObject({ counts: { choice: 0 }, loading: true, stale: true });
    await act(async () =>
      root.render(<Probe reference={{ ...property, type: PropertyType.SingleLineText }} />),
    );
    expect(latest).toMatchObject({ loading: false, error: false, stale: false });
    expect(latest.counts).toBeUndefined();
    expect(latest.source.getSnapshot()).toBeUndefined();
    await act(async () => {
      resourceChangedChannel.publish([2]);
      await vi.advanceTimersByTimeAsync(2000);
    });
    expect(request).toHaveBeenCalledTimes(1);
    await act(async () => root.render(<Probe />));
    expect(latest.loading).toBe(true);
    expect(latest.counts).toBeUndefined();
    await act(async () => {
      await vi.advanceTimersByTimeAsync(1000);
    });
    expect(latest).toMatchObject({ counts: { choice: 1 }, loading: false, stale: false });
  });

  it("keeps zero counts during notification bursts and uses one search debounce", async () => {
    request.mockResolvedValueOnce({ data: { isReady: true, counts: { choice: 0 } } });
    request.mockResolvedValueOnce({ data: { isReady: true, counts: { choice: 1 } } });
    await act(async () =>
      root.render(<Probe search={{ page: 1, pageSize: 50, keyword: "filtered" }} />),
    );
    await act(async () => {
      await vi.advanceTimersByTimeAsync(1000);
    });
    await act(async () => resourceChangedChannel.publish([1]));
    expect(latest).toMatchObject({ counts: { choice: 0 }, loading: true, stale: true });
    expect(latest.source.getSnapshot()).toEqual({ choice: 0 });
    await act(async () => {
      await vi.advanceTimersByTimeAsync(500);
      resourceChangedChannel.publish([2]);
    });
    await act(async () => {
      await vi.advanceTimersByTimeAsync(999);
    });
    expect(request).toHaveBeenCalledTimes(1);
    expect(latest.counts).toEqual({ choice: 0 });
    await act(async () => {
      await vi.advanceTimersByTimeAsync(1);
    });
    expect(request).toHaveBeenCalledTimes(2);
    expect(latest).toMatchObject({ counts: { choice: 1 }, loading: false, stale: false });
  });

  it("ignores old failures after a newer filter request succeeds", async () => {
    let rejectOld: (reason: unknown) => void = () => {};

    request.mockResolvedValueOnce({ data: { isReady: true, counts: { choice: 8 } } });
    request.mockImplementationOnce(
      () =>
        new Promise((_resolve, reject) => {
          rejectOld = reject;
        }),
    );
    request.mockResolvedValueOnce({ data: { isReady: true, counts: { choice: 3 } } });
    await act(async () => root.render(<Probe />));
    await act(async () => {
      await vi.advanceTimersByTimeAsync(0);
    });
    await act(async () =>
      root.render(<Probe search={{ page: 1, pageSize: 50, keyword: "old" }} />),
    );
    await act(async () => {
      await vi.advanceTimersByTimeAsync(1000);
    });
    await act(async () =>
      root.render(<Probe search={{ page: 1, pageSize: 50, keyword: "new" }} />),
    );
    await act(async () => {
      await vi.advanceTimersByTimeAsync(1000);
    });
    await act(async () => rejectOld(new Error("late failure")));
    expect(latest).toMatchObject({
      counts: { choice: 3 },
      loading: false,
      error: false,
      stale: false,
    });
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
    });
    await act(async () => {
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
