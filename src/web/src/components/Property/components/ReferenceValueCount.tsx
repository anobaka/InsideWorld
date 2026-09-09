import { useTranslation } from "react-i18next";

import {
  useResourceCountsPresentation,
  type ResourceCountsPresentation,
  type ResourceCountsSource,
} from "@/hooks/useResourceCountsSource";

function getStatusKey(presentation?: ResourceCountsPresentation) {
  if (presentation?.error) {
    return presentation.stale
      ? "property.reference.countsUpdateFailed"
      : "property.reference.countsLoadFailed";
  }

  if (presentation?.loading || presentation?.stale) {
    return presentation.stale
      ? "property.reference.updatingCounts"
      : "property.reference.loadingCounts";
  }
}

export function ReferenceValueCountsStatus({ source }: { source?: ResourceCountsSource }) {
  const { t } = useTranslation();
  const presentation = useResourceCountsPresentation(source);

  if (!presentation) return null;

  const statusKey = getStatusKey(presentation);
  const message = statusKey ? t(statusKey) : undefined;

  return (
    <div
      aria-atomic="true"
      aria-hidden={!message}
      aria-live="polite"
      className="relative h-4 shrink-0 text-xs leading-4 text-default-400"
      role="status"
      title={message}
    >
      <span className="absolute inset-x-0 top-0 truncate">{message}</span>
    </div>
  );
}

export default function ReferenceValueCount({
  count,
  source,
  valueId,
}: {
  count?: number;
  source?: ResourceCountsSource;
  valueId?: string;
}) {
  const { t } = useTranslation();
  const presentation = useResourceCountsPresentation(source);
  const reservedWidth = valueId === undefined ? undefined : presentation?.reservedWidths?.[valueId];
  const hasCount = count !== undefined && count > 0;

  if (!hasCount && reservedWidth === undefined) return null;

  const statusKey = getStatusKey(presentation);

  return (
    <span
      aria-hidden={!hasCount}
      className={`ml-1 text-xs tabular-nums ${presentation?.loading || presentation?.stale ? "opacity-40" : "opacity-70"}`}
      style={
        reservedWidth === undefined
          ? undefined
          : {
              display: "inline-block",
              width: `${reservedWidth}ch`,
              textAlign: "right",
              flexShrink: 0,
              whiteSpace: "nowrap",
              visibility: hasCount ? undefined : "hidden",
            }
      }
      title={
        hasCount
          ? statusKey
            ? t(statusKey)
            : t("property.reference.resourceCount", { count })
          : undefined
      }
    >
      {hasCount ? `(${count.toLocaleString()})` : undefined}
    </span>
  );
}
