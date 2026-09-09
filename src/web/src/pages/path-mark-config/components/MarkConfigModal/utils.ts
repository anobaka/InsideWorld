import type { MarkConfig } from "./types";

import {
  PathMarkType,
  PathMatchMode,
  PropertyValueType,
  PathMarkApplyScope,
} from "@/sdk/constants";

type PropertyMarkCompatibilityConfig = {
  matchMode?: PathMatchMode;
  layer?: number | null;
  regex?: string | null;
  applyScope?: PathMarkApplyScope;
  valueType?: PropertyValueType;
  valueLayer?: number | null;
  valueRegex?: string | null;
  valueRegexMatchesResourcePath?: boolean;
};

type RawMarkConfig = PropertyMarkCompatibilityConfig & {
  pool?: MarkConfig["propertyPool"];
  propertyId?: number;
  fixedValue?: string;
  fsTypeFilter?: MarkConfig["fsTypeFilter"];
  extensions?: string[];
  extensionGroupIds?: number[];
  isResourceBoundary?: boolean;
  mediaLibraryId?: number;
  layerToMediaLibrary?: number;
  regexToMediaLibrary?: string;
};

/**
 * V220 property marks did not persist an applicability selector. During the migration those marks
 * were applied to every resource under the mark path, so preserve that meaning when the legacy
 * shape is read by the current editor and preview.
 */
export const normalizeLegacyPropertyMarkConfig = <T extends PropertyMarkCompatibilityConfig>(
  config: T,
  markType: PathMarkType,
): T => {
  const hasLayer = config.layer !== undefined && config.layer !== null;
  const hasRegex = typeof config.regex === "string" && config.regex.length > 0;
  const hasValueLayer = config.valueLayer !== undefined && config.valueLayer !== null;
  const hasValueRegex = typeof config.valueRegex === "string" && config.valueRegex.length > 0;
  const isLegacyV220PropertyMark =
    markType === PathMarkType.Property &&
    config.valueType === PropertyValueType.Dynamic &&
    (config.applyScope ?? PathMarkApplyScope.MatchedOnly) === PathMarkApplyScope.MatchedOnly &&
    !hasLayer &&
    !hasRegex &&
    ((config.matchMode === PathMatchMode.Layer && hasValueLayer) ||
      (config.matchMode === PathMatchMode.Regex && hasValueRegex));

  if (!isLegacyV220PropertyMark) {
    return config;
  }

  return {
    ...config,
    matchMode: PathMatchMode.Layer,
    layer: 0,
    regex: undefined,
    applyScope: PathMarkApplyScope.MatchedAndSubdirectories,
    valueRegexMatchesResourcePath:
      config.matchMode === PathMatchMode.Regex && hasValueRegex
        ? true
        : config.valueRegexMatchesResourcePath,
  } as T;
};

export const parseMarkConfig = (configJson?: string, markType?: PathMarkType): MarkConfig => {
  try {
    const parsedConfig = JSON.parse(configJson || "{}") as RawMarkConfig;
    const isNewMark = configJson === undefined;
    // No configJson means a new mark. Its defaults must not be mistaken for a migrated V220 mark.
    const config =
      !isNewMark && markType !== undefined
        ? normalizeLegacyPropertyMarkConfig(parsedConfig, markType)
        : parsedConfig;

    return {
      matchMode: config.matchMode ?? PathMatchMode.Layer,
      layer: config.layer ?? (isNewMark ? 0 : undefined),
      regex: config.regex ?? (isNewMark ? "" : undefined),
      applyScope: config.applyScope ?? PathMarkApplyScope.MatchedOnly,
      propertyPool: config.pool,
      propertyId: config.propertyId,
      valueType: config.valueType ?? PropertyValueType.Fixed,
      fixedValue: config.fixedValue ?? "",
      valueMatchMode:
        config.valueRegexMatchesResourcePath || config.valueRegex
          ? PathMatchMode.Regex
          : PathMatchMode.Layer,
      valueLayer: config.valueLayer ?? (isNewMark ? 0 : undefined),
      valueRegex: config.valueRegex ?? "",
      valueRegexMatchesResourcePath: config.valueRegexMatchesResourcePath ?? false,
      fsTypeFilter: config.fsTypeFilter,
      extensions: config.extensions ?? [],
      extensionGroupIds: config.extensionGroupIds ?? [],
      isResourceBoundary: config.isResourceBoundary ?? false,
      mediaLibraryId: config.mediaLibraryId,
      mediaLibraryValueType: config.valueType ?? PropertyValueType.Fixed,
      layerToMediaLibrary: config.layerToMediaLibrary ?? 0,
      regexToMediaLibrary: config.regexToMediaLibrary ?? "",
    };
  } catch {
    return {
      matchMode: PathMatchMode.Layer,
      layer: 0,
      applyScope: PathMarkApplyScope.MatchedOnly,
      valueType: PropertyValueType.Fixed,
      valueMatchMode: PathMatchMode.Layer,
      valueLayer: 0,
    };
  }
};

export const hasValidPropertyValueExtractor = (
  config: MarkConfig,
  markType: PathMarkType,
): boolean => {
  if (markType !== PathMarkType.Property || config.valueType !== PropertyValueType.Dynamic) {
    return true;
  }

  if (config.valueMatchMode === PathMatchMode.Regex) {
    return Boolean(config.valueRegex?.trim());
  }

  return (
    config.valueMatchMode === PathMatchMode.Layer &&
    typeof config.valueLayer === "number" &&
    Number.isFinite(config.valueLayer)
  );
};

export const buildConfigJson = (config: MarkConfig, markType: PathMarkType): string => {
  if (markType === PathMarkType.Resource) {
    return JSON.stringify({
      matchMode: config.matchMode,
      layer: config.layer,
      regex: config.regex,
      fsTypeFilter: config.fsTypeFilter,
      extensions: config.extensions,
      extensionGroupIds: config.extensionGroupIds,
      applyScope: config.applyScope ?? PathMarkApplyScope.MatchedOnly,
      isResourceBoundary: config.isResourceBoundary ?? false,
    });
  } else if (markType === PathMarkType.MediaLibrary) {
    return JSON.stringify({
      matchMode: config.matchMode,
      layer: config.layer,
      regex: config.regex,
      valueType: config.mediaLibraryValueType,
      mediaLibraryId:
        config.mediaLibraryValueType === PropertyValueType.Fixed
          ? config.mediaLibraryId
          : undefined,
      layerToMediaLibrary:
        config.mediaLibraryValueType === PropertyValueType.Dynamic
          ? config.layerToMediaLibrary
          : undefined,
      regexToMediaLibrary:
        config.mediaLibraryValueType === PropertyValueType.Dynamic
          ? config.regexToMediaLibrary
          : undefined,
      applyScope: config.applyScope ?? PathMarkApplyScope.MatchedOnly,
    });
  } else {
    // Property type
    return JSON.stringify({
      matchMode: config.matchMode,
      layer: config.layer,
      regex: config.regex,
      pool: config.propertyPool,
      propertyId: config.propertyId,
      valueType: config.valueType,
      fixedValue: config.fixedValue,
      valueLayer: config.valueMatchMode === PathMatchMode.Regex ? undefined : config.valueLayer,
      valueRegex: config.valueMatchMode === PathMatchMode.Regex ? config.valueRegex : undefined,
      valueRegexMatchesResourcePath:
        config.valueType === PropertyValueType.Dynamic &&
        config.valueMatchMode === PathMatchMode.Regex &&
        config.valueRegexMatchesResourcePath
          ? true
          : undefined,
      applyScope: config.applyScope ?? PathMarkApplyScope.MatchedOnly,
    });
  }
};
