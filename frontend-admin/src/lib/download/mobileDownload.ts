/**
 * Mobile-oriented download helpers: detection, size/speed labels,
 * Web Share / Save to Files, and completion haptics.
 */

import type { CSSProperties } from 'react';

export const TOUCH_TARGET_MIN_PX = 44;

/** Offer "download in background" when expected size meets this (or forced). */
export const BACKGROUND_DOWNLOAD_MIN_BYTES = 10 * 1024 * 1024;

const MOBILE_UA = /Android|iPhone|iPad|iPod|Mobile|webOS|BlackBerry|IEMobile|Opera Mini/i;

export function isMobileUserAgent(ua = typeof navigator !== 'undefined' ? navigator.userAgent : ''): boolean {
  return MOBILE_UA.test(ua);
}

/**
 * Prefer touch/mobile download UX when the UA looks mobile or the primary
 * pointer is coarse (phones/tablets), or the viewport is phone-narrow.
 */
export function isMobileDownloadClient(): boolean {
  if (typeof globalThis.window === 'undefined') return false;
  if (isMobileUserAgent(globalThis.navigator?.userAgent ?? '')) return true;
  try {
    if (globalThis.window.matchMedia('(pointer: coarse)').matches) return true;
    if (globalThis.window.matchMedia('(max-width: 767px)').matches) return true;
  } catch {
    // matchMedia may throw in non-browser test shims
  }
  return false;
}

/**
 * Mobile-friendly file size: never show raw byte counts; prefer MB (and GB/KB).
 */
export function formatMobileFileSize(bytes: number, formatLocale: string): string {
  if (!Number.isFinite(bytes) || bytes < 0) return '—';
  if (bytes === 0) return '0 MB';

  const mb = bytes / (1024 * 1024);
  if (mb >= 1024) {
    const gb = mb / 1024;
    return `${gb.toLocaleString(formatLocale, { maximumFractionDigits: 2 })} GB`;
  }
  if (mb >= 0.1) {
    return `${mb.toLocaleString(formatLocale, { maximumFractionDigits: 1 })} MB`;
  }
  const kb = bytes / 1024;
  if (kb >= 1) {
    return `${kb.toLocaleString(formatLocale, { maximumFractionDigits: 0 })} KB`;
  }
  // Sub-kilobyte: still avoid "N B" — show as fraction of MB
  return `${mb.toLocaleString(formatLocale, { maximumFractionDigits: 3 })} MB`;
}

/**
 * Speed like "2,3 MB/s" / "450 KB/s" — never raw bytes/s.
 */
export function formatMobileSpeed(bytesPerSecond: number, formatLocale: string): string {
  if (!Number.isFinite(bytesPerSecond) || bytesPerSecond < 1) {
    return `0 KB/s`;
  }
  const mbps = bytesPerSecond / (1024 * 1024);
  if (mbps >= 0.1) {
    return `${mbps.toLocaleString(formatLocale, { maximumFractionDigits: 1 })} MB/s`;
  }
  const kbps = bytesPerSecond / 1024;
  return `${kbps.toLocaleString(formatLocale, { maximumFractionDigits: 0 })} KB/s`;
}

export function shouldOfferBackgroundDownload(
  sizeBytes: number | null | undefined,
  force = false
): boolean {
  if (force) return true;
  if (sizeBytes == null || !Number.isFinite(sizeBytes)) return false;
  return sizeBytes >= BACKGROUND_DOWNLOAD_MIN_BYTES;
}

export function canUseWebShareFiles(): boolean {
  if (typeof globalThis.navigator === 'undefined') return false;
  const nav = globalThis.navigator as Navigator & {
    canShare?: (data: ShareData) => boolean;
    share?: (data: ShareData) => Promise<void>;
  };
  if (typeof nav.share !== 'function') return false;
  try {
    const probe = new File([new Blob(['x'], { type: 'text/plain' })], 'probe.txt', {
      type: 'text/plain',
    });
    if (typeof nav.canShare === 'function') {
      return nav.canShare({ files: [probe] });
    }
    // Older Safari: share exists; file share may still work at call time
    return true;
  } catch {
    return false;
  }
}

export function canUseWebShare(): boolean {
  if (typeof globalThis.navigator === 'undefined') return false;
  return typeof (globalThis.navigator as Navigator).share === 'function';
}

/**
 * Opens the native share sheet (iOS/Android). Includes the file when supported
 * so "Save to Files" appears on iOS.
 */
export async function shareDownloadBlob(
  blob: Blob,
  fileName: string,
  options?: { title?: string; text?: string }
): Promise<'shared' | 'cancelled' | 'unsupported'> {
  if (!canUseWebShare()) return 'unsupported';

  const nav = globalThis.navigator as Navigator & {
    canShare?: (data: ShareData) => boolean;
    share: (data: ShareData) => Promise<void>;
  };

  const mime = blob.type || 'application/octet-stream';
  const file = new File([blob], fileName, { type: mime });
  const withFiles: ShareData = {
    files: [file],
    title: options?.title ?? fileName,
    text: options?.text,
  };
  const textOnly: ShareData = {
    title: options?.title ?? fileName,
    text: options?.text ?? fileName,
  };

  try {
    const data =
      typeof nav.canShare === 'function' && nav.canShare(withFiles) ? withFiles : textOnly;
    if (typeof nav.canShare === 'function' && !nav.canShare(data)) {
      return 'unsupported';
    }
    await nav.share(data);
    return 'shared';
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') {
      return 'cancelled';
    }
    // Retry text-only if file share failed
    try {
      await nav.share(textOnly);
      return 'shared';
    } catch (inner) {
      if (inner instanceof DOMException && inner.name === 'AbortError') {
        return 'cancelled';
      }
      throw inner;
    }
  }
}

/**
 * Mobile "Save to Files": prefer share sheet (iOS Files / Android save targets),
 * else fall back to the browser download prompt.
 */
export async function saveBlobToFiles(
  blob: Blob,
  fileName: string,
  fallbackDownload: (b: Blob, name: string) => void
): Promise<'saved' | 'shared' | 'cancelled' | 'downloaded'> {
  if (canUseWebShareFiles()) {
    const result = await shareDownloadBlob(blob, fileName);
    if (result === 'shared') return 'shared';
    if (result === 'cancelled') return 'cancelled';
  }
  fallbackDownload(blob, fileName);
  return 'downloaded';
}

/** Vibration pattern for successful download (no-op when unsupported, e.g. iOS Safari). */
export function hapticDownloadComplete(): void {
  try {
    const nav = globalThis.navigator as Navigator & {
      vibrate?: (pattern: number | number[]) => boolean;
    };
    if (typeof nav.vibrate === 'function') {
      nav.vibrate([12, 40, 18]);
    }
  } catch {
    // Ignore — haptics are best-effort
  }
}

export function touchFriendlyButtonStyle(extra?: CSSProperties): CSSProperties {
  return {
    minHeight: TOUCH_TARGET_MIN_PX,
    minWidth: TOUCH_TARGET_MIN_PX,
    ...extra,
  };
}
