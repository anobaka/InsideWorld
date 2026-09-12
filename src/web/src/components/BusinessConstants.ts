const pathSeparator = "/";

export default {
  pathSeparator,
  uncPathPrefix: pathSeparator + pathSeparator,
  DefaultResourceColumnCount: 6,
  // Mirrors InternalOptions.MaxThumbnailWidth/Height on the server: covers it
  // generates are already capped at this size, so asking for it is a no-op for
  // them and only downscales oversized manual or enhancer-supplied covers.
  MaxCoverSize: 600,
};
