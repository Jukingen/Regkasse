/**
 * Browser helpers for export downloads (anchor download + File System Access API).
 */

export function triggerBlobDownload(blob: Blob, fileName: string): void {
  const url = globalThis.URL.createObjectURL(blob);
  const anchor = globalThis.document.createElement('a');
  anchor.href = url;
  anchor.download = fileName;
  anchor.click();
  globalThis.URL.revokeObjectURL(url);
}

/** Chromium desktop File System Access API — lets the user pick a save location. */
export function canSaveBlobToFolder(): boolean {
  if (typeof globalThis.window === 'undefined') return false;
  if (!('showSaveFilePicker' in globalThis.window)) return false;
  // Coarse mobile heuristic — folder picker is a desktop affordance.
  const ua = globalThis.navigator?.userAgent ?? '';
  if (/Android|iPhone|iPad|iPod|Mobile/i.test(ua)) return false;
  return true;
}

type SaveFilePickerWindow = Window & {
  showSaveFilePicker: (options?: {
    suggestedName?: string;
    types?: Array<{
      description?: string;
      accept: Record<string, string[]>;
    }>;
  }) => Promise<FileSystemFileHandle>;
};

export async function saveBlobToFolder(blob: Blob, fileName: string): Promise<'saved' | 'cancelled'> {
  if (!canSaveBlobToFolder()) {
    triggerBlobDownload(blob, fileName);
    return 'saved';
  }

  const w = globalThis.window as SaveFilePickerWindow;
  const extension = fileName.includes('.') ? `.${fileName.split('.').pop()}` : '';
  const mime = blob.type || 'application/octet-stream';

  try {
    const handle = await w.showSaveFilePicker({
      suggestedName: fileName,
      types: [
        {
          description: extension ? extension.slice(1).toUpperCase() : 'File',
          accept: { [mime]: extension ? [extension] : [] },
        },
      ],
    });
    const writable = await handle.createWritable();
    await writable.write(blob);
    await writable.close();
    return 'saved';
  } catch (error) {
    // User dismissed the picker (AbortError) — treat as cancel, not failure.
    if (error instanceof DOMException && error.name === 'AbortError') {
      return 'cancelled';
    }
    throw error;
  }
}

export function estimateJsonByteSize(data: unknown): number {
  return new TextEncoder().encode(JSON.stringify(data, null, 2)).length;
}

export function createJsonExportBlob(data: unknown): Blob {
  return new Blob([JSON.stringify(data, null, 2)], { type: 'application/json' });
}

