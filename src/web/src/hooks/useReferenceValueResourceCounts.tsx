import type { PropsWithChildren } from "react";
import type { ReferenceProperty } from "@/components/Property/referenceValues";
import type { SearchForm } from "@/pages/resource/models";

import { createContext, useContext, useEffect, useMemo, useState } from "react";

import BApi from "@/sdk/BApi";
import { ContentType } from "@/sdk/Api";
import { InternalProperty, PropertyPool } from "@/sdk/constants";
import { isReferenceValueType } from "@/components/Property/PropertySystem";
import { toSearchInputModel } from "@/components/ResourceFilter/utils/toInputModel";
import { resourceChangedChannel } from "@/services/ResourceChangedChannel";

type CountsResponse = { counts: Record<string, number>; isReady: boolean };
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
  const propertyKey = enabled ? `${property.pool}:${property.id}:${property.type}` : "";
  const key = enabled ? `${propertyKey}:${criteria}` : "";
  const [refreshRequest, setRefreshRequest] = useState({
    revision: 0,
    debouncePropertyKey: "",
  });
  const requestToken = useMemo(() => ({}), [key, refreshRequest.revision]);
  const [result, setResult] = useState<{
    propertyKey: string;
    requestToken: object;
    counts?: Record<string, number>;
    error?: boolean;
  }>();

  useEffect(() => {
    if (!enabled) return;

    return resourceChangedChannel.subscribe(() => {
      // Mark the counts stale immediately; the request effect coalesces bursts.
      setRefreshRequest(({ revision }) => ({
        revision: revision + 1,
        debouncePropertyKey: propertyKey,
      }));
    });
  }, [enabled, propertyKey]);

  useEffect(() => {
    // Never retain another property's counts, including across disabled periods.
    setResult((previous) => (previous?.propertyKey === propertyKey ? previous : undefined));
    if (!enabled || !property) return;
    let cancelled = false;
    let timer: ReturnType<typeof setTimeout>;
    const run = async () => {
      try {
        const response = await loadCounts(key, property, JSON.parse(criteria));

        if (cancelled) return;
        if (response.isReady) {
          setResult({ propertyKey, requestToken, counts: response.counts });
        } else {
          // Index warming must not be presented as zero resources.
          timer = setTimeout(run, 2000);
        }
      } catch {
        if (!cancelled)
          setResult((previous) => ({
            propertyKey,
            requestToken,
            counts: previous?.propertyKey === propertyKey ? previous.counts : undefined,
            error: true,
          }));
      }
    };

    timer = setTimeout(
      run,
      search || refreshRequest.debouncePropertyKey === propertyKey ? 1000 : 0,
    );

    return () => {
      cancelled = true;
      clearTimeout(timer);
    };
  }, [requestToken]);

  const current = enabled && result?.propertyKey === propertyKey ? result : undefined;
  const settled = current?.requestToken === requestToken;
  const loading = enabled && !settled;
  const error = enabled && settled && !!current?.error;

  return {
    counts: current?.counts,
    error,
    loading,
    stale: current?.counts !== undefined && (loading || error),
    refresh: () => {
      setRefreshRequest(({ revision }) => ({ revision: revision + 1, debouncePropertyKey: "" }));
    },
  };
}
