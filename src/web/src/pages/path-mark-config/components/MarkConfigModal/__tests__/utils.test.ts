import { describe, expect, it } from "vitest";

import {
  buildConfigJson,
  hasValidPropertyValueExtractor,
  normalizeLegacyPropertyMarkConfig,
  parseMarkConfig,
} from "../utils";

import {
  PathMarkApplyScope,
  PathMarkType,
  PathMatchMode,
  PropertyValueType,
} from "@/sdk/constants";

const legacyPropertyConfig = {
  matchMode: PathMatchMode.Layer,
  pool: 4,
  propertyId: 52,
  valueType: PropertyValueType.Dynamic,
  valueLayer: -1,
  applyScope: PathMarkApplyScope.MatchedOnly,
};

describe("legacy V220 property mark compatibility", () => {
  it("restores the original all-resources-under-path applicability", () => {
    const config = parseMarkConfig(JSON.stringify(legacyPropertyConfig), PathMarkType.Property);

    expect(config).toMatchObject({
      matchMode: PathMatchMode.Layer,
      layer: 0,
      applyScope: PathMarkApplyScope.MatchedAndSubdirectories,
      propertyPool: 4,
      propertyId: 52,
      valueType: PropertyValueType.Dynamic,
      valueLayer: -1,
    });
  });

  it("persists an explicit selector instead of layer zero plus matched-only", () => {
    const config = parseMarkConfig(JSON.stringify(legacyPropertyConfig), PathMarkType.Property);
    const configJson = buildConfigJson(config, PathMarkType.Property);

    expect(JSON.parse(configJson)).toMatchObject({
      matchMode: PathMatchMode.Layer,
      layer: 0,
      applyScope: PathMarkApplyScope.MatchedAndSubdirectories,
      pool: 4,
      propertyId: 52,
      valueLayer: -1,
    });
  });

  it("preserves a legacy regex value extractor while canonicalizing its selector", () => {
    const config = parseMarkConfig(
      JSON.stringify({
        ...legacyPropertyConfig,
        matchMode: PathMatchMode.Regex,
        valueLayer: undefined,
        valueRegex: "^([^/]+)$",
      }),
      PathMarkType.Property,
    );
    const savedConfig = JSON.parse(buildConfigJson(config, PathMarkType.Property));

    expect(config).toMatchObject({
      matchMode: PathMatchMode.Layer,
      layer: 0,
      applyScope: PathMarkApplyScope.MatchedAndSubdirectories,
      valueMatchMode: PathMatchMode.Regex,
      valueRegex: "^([^/]+)$",
    });
    expect(savedConfig).toMatchObject({
      matchMode: PathMatchMode.Layer,
      layer: 0,
      applyScope: PathMarkApplyScope.MatchedAndSubdirectories,
      valueRegex: "^([^/]+)$",
      valueRegexMatchesResourcePath: true,
    });
    expect(savedConfig).not.toHaveProperty("valueLayer");
  });

  it("treats an omitted V220 apply scope as its backend default", () => {
    const config = parseMarkConfig(
      JSON.stringify({
        ...legacyPropertyConfig,
        applyScope: undefined,
      }),
      PathMarkType.Property,
    );

    expect(config).toMatchObject({
      matchMode: PathMatchMode.Layer,
      layer: 0,
      applyScope: PathMarkApplyScope.MatchedAndSubdirectories,
      valueLayer: -1,
    });
  });

  it("does not expand an explicit modern property selector", () => {
    const config = parseMarkConfig(
      JSON.stringify({
        ...legacyPropertyConfig,
        layer: 0,
      }),
      PathMarkType.Property,
    );

    expect(config.layer).toBe(0);
    expect(config.applyScope).toBe(PathMarkApplyScope.MatchedOnly);
  });

  it("does not apply property compatibility to other mark types", () => {
    const config = normalizeLegacyPropertyMarkConfig(
      { ...legacyPropertyConfig },
      PathMarkType.MediaLibrary,
    );

    expect(config).toEqual(legacyPropertyConfig);
  });

  it("requires the V220 match mode and value extractor to agree", () => {
    const unknownMode = normalizeLegacyPropertyMarkConfig(
      { ...legacyPropertyConfig, matchMode: 0 as PathMatchMode },
      PathMarkType.Property,
    );
    const mismatchedLayer = normalizeLegacyPropertyMarkConfig(
      {
        ...legacyPropertyConfig,
        matchMode: PathMatchMode.Regex,
      },
      PathMarkType.Property,
    );
    const mismatchedRegex = normalizeLegacyPropertyMarkConfig(
      {
        ...legacyPropertyConfig,
        matchMode: PathMatchMode.Layer,
        valueLayer: undefined,
        valueRegex: "^(AV)$",
      },
      PathMarkType.Property,
    );

    expect(unknownMode).toEqual({ ...legacyPropertyConfig, matchMode: 0 });
    expect(mismatchedLayer).toEqual({
      ...legacyPropertyConfig,
      matchMode: PathMatchMode.Regex,
    });
    expect(mismatchedRegex).toEqual({
      ...legacyPropertyConfig,
      matchMode: PathMatchMode.Layer,
      valueLayer: undefined,
      valueRegex: "^(AV)$",
    });
  });

  it("does not repair malformed or fixed-value property configs while saving", () => {
    const rawFixedConfig = {
      ...legacyPropertyConfig,
      valueType: PropertyValueType.Fixed,
    };
    const rawMissingExtractorConfig = {
      ...legacyPropertyConfig,
      valueLayer: undefined,
    };
    const fixedConfig = JSON.parse(
      buildConfigJson(
        parseMarkConfig(JSON.stringify(rawFixedConfig), PathMarkType.Property),
        PathMarkType.Property,
      ),
    );
    const missingExtractorConfig = JSON.parse(
      buildConfigJson(
        parseMarkConfig(JSON.stringify(rawMissingExtractorConfig), PathMarkType.Property),
        PathMarkType.Property,
      ),
    );

    expect(fixedConfig).not.toHaveProperty("layer");
    expect(fixedConfig).not.toHaveProperty("regex");
    expect(missingExtractorConfig).not.toHaveProperty("layer");
    expect(missingExtractorConfig).not.toHaveProperty("regex");
    expect(missingExtractorConfig).not.toHaveProperty("valueLayer");
    expect(missingExtractorConfig).not.toHaveProperty("valueRegex");
  });

  it("keeps the existing defaults for a newly created property mark", () => {
    const config = parseMarkConfig(undefined, PathMarkType.Property);

    expect(config.layer).toBe(0);
    expect(config.valueLayer).toBe(0);
    expect(config.applyScope).toBe(PathMarkApplyScope.MatchedOnly);
  });

  it("keeps a cleared migrated regex in regex mode and rejects saving it", () => {
    const config = parseMarkConfig(
      JSON.stringify({
        ...legacyPropertyConfig,
        matchMode: PathMatchMode.Layer,
        layer: 0,
        valueLayer: undefined,
        valueRegex: "",
        valueRegexMatchesResourcePath: true,
        applyScope: PathMarkApplyScope.MatchedAndSubdirectories,
      }),
      PathMarkType.Property,
    );

    expect(config.valueMatchMode).toBe(PathMatchMode.Regex);
    expect(hasValidPropertyValueExtractor(config, PathMarkType.Property)).toBe(false);
  });

  it("requires an explicit extractor only for dynamic property marks", () => {
    const missingLayer = {
      ...parseMarkConfig(JSON.stringify({}), PathMarkType.Property),
      valueType: PropertyValueType.Dynamic,
      valueMatchMode: PathMatchMode.Layer,
      valueLayer: undefined,
    };

    expect(hasValidPropertyValueExtractor(missingLayer, PathMarkType.Property)).toBe(false);
    expect(
      hasValidPropertyValueExtractor({ ...missingLayer, valueLayer: 0 }, PathMarkType.Property),
    ).toBe(true);
    expect(
      hasValidPropertyValueExtractor(
        { ...missingLayer, valueLayer: Number.NaN },
        PathMarkType.Property,
      ),
    ).toBe(false);
    expect(
      hasValidPropertyValueExtractor(
        { ...missingLayer, valueMatchMode: PathMatchMode.Regex, valueRegex: "^(AV)$" },
        PathMarkType.Property,
      ),
    ).toBe(true);
    expect(hasValidPropertyValueExtractor(missingLayer, PathMarkType.MediaLibrary)).toBe(true);
  });
});
