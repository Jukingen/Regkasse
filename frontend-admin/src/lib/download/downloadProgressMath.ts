/**
 * Pure helpers for download progress UI (speed + ETA).
 */

export const DOWNLOAD_PROGRESS_MIN_BYTES = 5 * 1024 * 1024;
export const DOWNLOAD_RESUME_CHUNK_BYTES = 4 * 1024 * 1024;

export function shouldShowDownloadProgress(
  sizeBytes: number | null | undefined,
  force = false
): boolean {
  if (force) return true;
  if (sizeBytes == null || !Number.isFinite(sizeBytes)) return false;
  return sizeBytes >= DOWNLOAD_PROGRESS_MIN_BYTES;
}

export function clampPercent(loaded: number, total: number | null | undefined): number {
  if (total == null || !Number.isFinite(total) || total <= 0) return 0;
  if (!Number.isFinite(loaded) || loaded <= 0) return 0;
  return Math.min(100, Math.max(0, Math.round((loaded / total) * 100)));
}

/** Exponential-ish windowed bytes/sec from monotonic loaded samples. */
export class DownloadSpeedTracker {
  private samples: Array<{ t: number; bytes: number }> = [];
  private readonly windowMs: number;

  constructor(windowMs = 2500) {
    this.windowMs = windowMs;
  }

  reset(): void {
    this.samples = [];
  }

  update(loadedBytes: number, now = Date.now()): number {
    this.samples.push({ t: now, bytes: loadedBytes });
    const cutoff = now - this.windowMs;
    while (this.samples.length > 2 && this.samples[0]!.t < cutoff) {
      this.samples.shift();
    }
    return this.bytesPerSecond(now);
  }

  bytesPerSecond(now = Date.now()): number {
    if (this.samples.length < 2) return 0;
    const first = this.samples[0]!;
    const last = this.samples[this.samples.length - 1]!;
    const dt = (Math.max(now, last.t) - first.t) / 1000;
    if (dt <= 0.05) return 0;
    const delta = last.bytes - first.bytes;
    if (delta <= 0) return 0;
    return delta / dt;
  }
}

export function estimateEtaSeconds(
  remainingBytes: number,
  bytesPerSecond: number
): number | null {
  if (!Number.isFinite(remainingBytes) || remainingBytes <= 0) return 0;
  if (!Number.isFinite(bytesPerSecond) || bytesPerSecond < 256) return null;
  return Math.ceil(remainingBytes / bytesPerSecond);
}

/** Format like "45 MB/s" using a byte formatter (e.g. formatBytes). */
export function formatSpeedLabel(
  bytesPerSecond: number,
  formatBytesFn: (n: number) => string
): string {
  if (!Number.isFinite(bytesPerSecond) || bytesPerSecond < 1) {
    return `${formatBytesFn(0)}/s`;
  }
  return `${formatBytesFn(Math.max(0, Math.round(bytesPerSecond)))}/s`;
}

export function formatEtaLabel(
  etaSeconds: number | null,
  t: (key: string, vars?: Record<string, string | number>) => string
): string {
  if (etaSeconds == null) return t('common.downloadProgress.etaUnknown');
  if (etaSeconds <= 0) return t('common.downloadProgress.etaDone');
  if (etaSeconds < 60) {
    return t('common.downloadProgress.etaSeconds', { count: etaSeconds });
  }
  const minutes = Math.ceil(etaSeconds / 60);
  if (minutes < 60) {
    return t('common.downloadProgress.etaMinutes', { count: minutes });
  }
  const hours = Math.floor(minutes / 60);
  const remMin = minutes % 60;
  return t('common.downloadProgress.etaHours', { hours, minutes: remMin });
}

export function isNetworkDownloadError(err: unknown): boolean {
  if (err == null || typeof err !== 'object') return false;
  const e = err as {
    code?: string;
    name?: string;
    message?: string;
    isAxiosError?: boolean;
    response?: unknown;
  };
  if (e.name === 'AbortError' || e.code === 'ERR_CANCELED') return false;
  if (e.isAxiosError && e.response == null) return true;
  if (e.code === 'ERR_NETWORK' || e.code === 'ECONNABORTED') return true;
  const msg = (e.message ?? '').toLowerCase();
  return msg.includes('network') || msg.includes('failed to fetch');
}
