/**
 * Shared helpers for pre-download export preview (size estimates + large-file gating).
 */

/** Warn / show “may take a moment” when estimated or known size exceeds this. */
export const LARGE_EXPORT_BYTES = 100 * 1024 * 1024;

export function isLargeExport(sizeBytes: number | undefined | null): boolean {
  if (sizeBytes == null || !Number.isFinite(sizeBytes)) return false;
  return sizeBytes > LARGE_EXPORT_BYTES;
}

/**
 * Rough byte estimate for tabular catalog exports before the server generates the file.
 * Used only for UX preview — not a substitute for Content-Length.
 */
export function estimateTabularExportBytes(
  rowCount: number,
  format: 'csv' | 'json' | string
): number {
  const rows = Math.max(0, Math.floor(rowCount));
  const normalized = String(format).trim().toLowerCase() === 'json' ? 'json' : 'csv';
  const perRow = normalized === 'json' ? 320 : 160;
  const overhead = normalized === 'json' ? 64 : 128;
  return rows * perRow + overhead;
}

/** Prefer known blob size; fall back to estimate when generation has not run yet. */
export function resolvePreviewSizeBytes(options: {
  knownBytes?: number | null;
  estimatedBytes?: number | null;
}): { sizeBytes: number; isEstimate: boolean } {
  if (options.knownBytes != null && Number.isFinite(options.knownBytes) && options.knownBytes >= 0) {
    return { sizeBytes: options.knownBytes, isEstimate: false };
  }
  const estimated = options.estimatedBytes ?? 0;
  return {
    sizeBytes: Number.isFinite(estimated) && estimated > 0 ? estimated : 0,
    isEstimate: true,
  };
}
