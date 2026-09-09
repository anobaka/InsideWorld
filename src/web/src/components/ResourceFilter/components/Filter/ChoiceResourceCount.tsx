import type {
  ChoiceResourceCountsState,
  ChoiceResourceCountsStore,
} from "../../hooks/choiceResourceCountsStore";

import { useTranslation } from "react-i18next";

import { useChoiceResourceCounts } from "../../hooks/choiceResourceCountsStore";

function getStatusKey(state: ChoiceResourceCountsState) {
  if (state.error) {
    return state.stale
      ? "property.reference.countsUpdateFailed"
      : "property.reference.countsLoadFailed";
  }

  if (state.loading || state.stale) {
    return state.stale ? "property.reference.updatingCounts" : "property.reference.loadingCounts";
  }
}

export function ChoiceResourceCountsStatus({ store }: { store: ChoiceResourceCountsStore }) {
  const { t } = useTranslation();
  const state = useChoiceResourceCounts(store);

  if (state.counts === undefined && !state.loading && !state.error) return null;
  const statusKey = getStatusKey(state);
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

/** A read-only extra label supplied by ResourceFilter, including its own live subscription. */
export default function ChoiceResourceCount({
  choiceId,
  store,
}: {
  choiceId: string;
  store: ChoiceResourceCountsStore;
}) {
  const { t } = useTranslation();
  const state = useChoiceResourceCounts(store);
  const count = state.counts?.[choiceId];
  const reservedWidth = state.reservedWidths?.[choiceId];
  const hasCount = count !== undefined && count > 0;

  if (!hasCount && reservedWidth === undefined) return null;
  const statusKey = getStatusKey(state);

  return (
    <span
      aria-hidden={!hasCount}
      className={`ml-1 text-xs tabular-nums ${state.loading || state.stale ? "opacity-40" : "opacity-70"}`}
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
