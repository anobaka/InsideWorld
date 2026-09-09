import type { PropsWithChildren } from "react";
import type {
  ReferenceProperty,
  ReferenceValueResourceCounts,
} from "@/components/Property/referenceValues";
import type { SearchForm } from "@/pages/resource/models";

import { createContext, useContext, useEffect, useMemo, useState } from "react";

import { createResourceCountsSource } from "./useResourceCountsSource";

import BApi from "@/sdk/BApi";
import { ContentType } from "@/sdk/Api";
import { InternalProperty, PropertyPool } from "@/sdk/constants";
import { isReferenceValueType } from "@/components/Property/PropertySystem";
import { toSearchInputModel } from "@/components/ResourceFilter/utils/toInputModel";
import { resourceChangedChannel } from "@/services/ResourceChangedChannel";

type CountsResponse = { counts: ReferenceValueResourceCounts; isReady: boolean };
const pending = new Map<string, Promise<CountsResponse>>();
const SearchContext = createContext<SearchForm | undefined>(undefined);

export function ReferenceValueSearchProvider({
  search,
  children,
}: PropsWithChildren<{ search: SearchForm }>) {
  return <SearchContext.Provider value={search}>{children}</SearchContext.Provider>;
}

export function useReferenceValueSearch() {
  return useContext(SearchContext);
}

export function referenceValueCountsCriteria(search?: SearchForm) {
  if (!search) return {};
  const { group, keyword, tags } = toSearchInputModel(search);

  // Pagination and ordering do not change the matching resource set.
  return { group, keyword, tags };
}

function loadCounts(key: string, property: ReferenceProperty, criteria: object) {
  let request = pending.get(key);

  if (!request) {
    request = BApi.request<{ code?: number; data?: CountsResponse; message?: string }, unknown>({
      path: `/property/pool/${property.pool}/id/${property.id}/value-resource-counts`,
      method: "POST",
      type: ContentType.Json,
      body: criteria,
      format: "json",
    })
      .then((response) => {
        if (response.code || !response.data)
          throw new Error(response.message || "Failed to load resource counts");

        return response.data;
      })
      .finally(() => pending.delete(key));
    pending.set(key, request);
  }

  return request;
}

/** One request per property, with shared in-flight requests and debounced criteria. */
export function useReferenceValueResourceCounts(property?: ReferenceProperty, search?: SearchForm) {
  const enabled =
    !!property?.id &&
    property.type != undefined &&
    isReferenceValueType(property.type) &&
    !(
      property.pool === PropertyPool.Internal &&
      [InternalProperty.ParentResource, InternalProperty.MediaLibraryV2].includes(property.id)
    );
  const criteria = JSON.stringify(referenceValueCountsCriteria(search));
  const key = enabled ? `${property.pool}:${property.id}:${property.type}:${criteria}` : "";
  const [result, setResult] = useState<{
    key: string;
    counts?: ReferenceValueResourceCounts;
    error?: boolean;
  }>();
  const [revision, setRevision] = useState(0);
  const source = useMemo(createResourceCountsSource, []);

  useEffect(() => {
    if (!enabled) return;
    let timer: ReturnType<typeof setTimeout>;
    const unsubscribe = resourceChangedChannel.subscribe(() => {
      clearTimeout(timer);
      timer = setTimeout(() => setRevision((value) => value + 1), 1000);
    });

    return () => {
      unsubscribe();
      clearTimeout(timer);
    };
  }, [enabled]);

  useEffect(() => {
    if (!enabled || !property) return;
    let cancelled = false;
    let timer: ReturnType<typeof setTimeout>;
    const run = async () => {
      try {
        const response = await loadCounts(key, property, JSON.parse(criteria));

        if (cancelled) return;
        if (response.isReady) {
          setResult({ key, counts: response.counts });
        } else {
          // Index warming must not be presented as zero resources.
          timer = setTimeout(run, 2000);
        }
      } catch {
        if (!cancelled) setResult({ key, error: true });
      }
    };

    timer = setTimeout(run, search ? 1000 : 0);

    return () => {
      cancelled = true;
      clearTimeout(timer);
    };
  }, [key, enabled, revision]);

  const current = result?.key === key ? result : undefined;

  useEffect(() => source.publish(current?.counts), [source, current?.counts]);

  return {
    source,
    counts: current?.counts,
    error: current?.error ?? false,
    loading: enabled && !current?.counts && !current?.error,
    refresh: () => {
      setResult(undefined);
      setRevision((value) => value + 1);
    },
  };
}
