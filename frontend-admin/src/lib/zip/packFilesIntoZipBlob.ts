/**
 * Client-side ZIP packing via dynamically imported `jszip`.
 * Uses `streamFiles` to lower peak memory while generating (data descriptors).
 */

export type ZipFileEntry = {
  path: string;
  data: Blob | ArrayBuffer | Uint8Array | string;
};

export type PackFilesIntoZipBlobOptions = {
  /** 0–100 while JSZip compresses/packs. */
  onProgress?: (percent: number) => void;
  /**
   * Stream entries and emit data descriptors (less peak memory).
   * Default true — some ancient unzip tools may dislike descriptors; modern tools are fine.
   */
  streamFiles?: boolean;
  /** DEFLATE level 1–9; default 1 (faster / lighter for large fiscal JSON). */
  compressionLevel?: number;
};

export class ZipPackError extends Error {
  readonly code: 'ZIP_NO_ENTRIES' | 'ZIP_GENERATE_FAILED';

  constructor(code: ZipPackError['code'], message: string, cause?: unknown) {
    super(message, cause !== undefined ? { cause } : undefined);
    this.name = 'ZipPackError';
    this.code = code;
  }
}

export async function packFilesIntoZipBlob(
  entries: readonly ZipFileEntry[],
  options: PackFilesIntoZipBlobOptions = {}
): Promise<Blob> {
  if (entries.length === 0) {
    throw new ZipPackError('ZIP_NO_ENTRIES', 'Cannot create an empty ZIP archive.');
  }

  const { default: JSZip } = await import('jszip');
  const zip = new JSZip();

  for (const entry of entries) {
    const path = entry.path.trim();
    if (!path) continue;
    zip.file(path, entry.data);
  }

  const fileCount = Object.keys(zip.files).filter((name) => !zip.files[name]?.dir).length;
  if (fileCount === 0) {
    throw new ZipPackError('ZIP_NO_ENTRIES', 'Cannot create an empty ZIP archive.');
  }

  try {
    return await zip.generateAsync(
      {
        type: 'blob',
        streamFiles: options.streamFiles ?? true,
        compression: 'DEFLATE',
        compressionOptions: { level: options.compressionLevel ?? 1 },
        mimeType: 'application/zip',
      },
      (metadata) => {
        const percent = Number.isFinite(metadata.percent) ? metadata.percent : 0;
        options.onProgress?.(Math.max(0, Math.min(100, percent)));
      }
    );
  } catch (cause) {
    throw new ZipPackError('ZIP_GENERATE_FAILED', 'ZIP generation failed.', cause);
  }
}

/** Trigger a one-shot browser download without navigating away. */
export function triggerBrowserDownload(blob: Blob, fileName: string): void {
  const url = globalThis.URL.createObjectURL(blob);
  try {
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = fileName;
    anchor.rel = 'noopener';
    anchor.style.display = 'none';
    document.body.appendChild(anchor);
    anchor.click();
    anchor.remove();
  } finally {
    // Allow the browser to start the download before revoking.
    globalThis.setTimeout(() => globalThis.URL.revokeObjectURL(url), 2_000);
  }
}
