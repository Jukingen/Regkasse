/**
 * Chunked HTTP download with pause/resume (Range) and progress callbacks.
 */

import type { AxiosInstance, AxiosRequestConfig } from 'axios';
import axios from 'axios';

import {
  DOWNLOAD_RESUME_CHUNK_BYTES,
  DownloadSpeedTracker,
  clampPercent,
  estimateEtaSeconds,
  isNetworkDownloadError,
} from '@/lib/download/downloadProgressMath';
import { AXIOS_INSTANCE } from '@/lib/axios';

export type ProgressiveDownloadPhase =
  | 'idle'
  | 'starting'
  | 'downloading'
  | 'paused'
  | 'error'
  | 'done'
  | 'cancelled';

export type ProgressiveDownloadErrorKind = 'network' | 'http' | 'cancelled' | 'unknown';

export type ProgressiveDownloadSnapshot = {
  phase: ProgressiveDownloadPhase;
  fileName: string;
  /** Short product label shown in title, e.g. "Backup". */
  label?: string;
  loadedBytes: number;
  totalBytes: number | null;
  percent: number;
  bytesPerSecond: number;
  etaSeconds: number | null;
  supportsPause: boolean;
  errorKind?: ProgressiveDownloadErrorKind;
  errorMessage?: string;
};

export class ProgressiveDownloadCancelledError extends Error {
  constructor(message = 'Download cancelled') {
    super(message);
    this.name = 'ProgressiveDownloadCancelledError';
  }
}

export class ProgressiveDownloadSession {
  private paused = false;
  private cancelled = false;
  private pauseWaiters: Array<() => void> = [];
  private chunkAbort: AbortController | null = null;
  readonly abortController = new AbortController();

  get signal(): AbortSignal {
    return this.abortController.signal;
  }

  get isPaused(): boolean {
    return this.paused;
  }

  get isCancelled(): boolean {
    return this.cancelled;
  }

  pause(): void {
    if (this.cancelled || this.paused) return;
    this.paused = true;
    this.chunkAbort?.abort();
  }

  resume(): void {
    if (this.cancelled || !this.paused) return;
    this.paused = false;
    const waiters = this.pauseWaiters.splice(0);
    for (const w of waiters) w();
  }

  cancel(): void {
    this.cancelled = true;
    this.paused = false;
    const waiters = this.pauseWaiters.splice(0);
    for (const w of waiters) w();
    this.chunkAbort?.abort();
    this.abortController.abort();
  }

  bindChunkAbort(controller: AbortController): void {
    this.chunkAbort = controller;
  }

  async waitWhilePaused(): Promise<void> {
    while (this.paused && !this.cancelled) {
      await new Promise<void>((resolve) => {
        this.pauseWaiters.push(resolve);
      });
    }
    if (this.cancelled) {
      throw new ProgressiveDownloadCancelledError();
    }
  }

  throwIfCancelled(): void {
    if (this.cancelled || this.signal.aborted) {
      throw new ProgressiveDownloadCancelledError();
    }
  }
}

export type ProgressiveDownloadRequest = {
  url: string;
  fileName: string;
  label?: string;
  expectedTotalBytes?: number | null;
  chunkSizeBytes?: number;
  session: ProgressiveDownloadSession;
  onProgress: (snapshot: ProgressiveDownloadSnapshot) => void;
  axiosInstance?: AxiosInstance;
  /** Extra axios config (auth headers come from AXIOS_INSTANCE interceptors). */
  axiosConfig?: Omit<
    AxiosRequestConfig,
    'url' | 'method' | 'responseType' | 'signal' | 'onDownloadProgress' | 'headers'
  > & { headers?: Record<string, string> };
};

function emit(
  onProgress: ProgressiveDownloadRequest['onProgress'],
  partial: ProgressiveDownloadSnapshot
): void {
  onProgress(partial);
}

function parseContentLength(headers: Record<string, unknown>): number | null {
  const raw = headers['content-length'] ?? headers['Content-Length'];
  if (raw == null) return null;
  const n = Number(raw);
  return Number.isFinite(n) && n >= 0 ? n : null;
}

function parseAcceptRanges(headers: Record<string, unknown>): boolean {
  const raw = String(headers['accept-ranges'] ?? headers['Accept-Ranges'] ?? '').toLowerCase();
  return raw.includes('bytes');
}

function parseContentRangeTotal(headers: Record<string, unknown>): number | null {
  const raw = String(headers['content-range'] ?? headers['Content-Range'] ?? '');
  const m = /bytes\s+\d+-\d+\/(\d+|\*)/i.exec(raw);
  if (!m?.[1] || m[1] === '*') return null;
  const n = Number(m[1]);
  return Number.isFinite(n) && n >= 0 ? n : null;
}

export function parseFilenameFromContentDisposition(
  header: string | undefined,
  fallback: string
): string {
  if (!header) return fallback;
  const utf8 = /filename\*=UTF-8''([^;]+)/i.exec(header);
  if (utf8?.[1]) {
    try {
      return decodeURIComponent(utf8[1].trim());
    } catch {
      /* ignore */
    }
  }
  const quoted = /filename="([^"]+)"/i.exec(header);
  if (quoted?.[1]) return quoted[1].trim();
  const plain = /filename=([^;]+)/i.exec(header);
  if (plain?.[1]) return plain[1].trim().replace(/^"|"$/g, '');
  return fallback;
}

async function probeRangeSupport(
  client: AxiosInstance,
  url: string,
  session: ProgressiveDownloadSession,
  baseHeaders: Record<string, string> | undefined
): Promise<{ total: number | null; supportsRange: boolean }> {
  try {
    const head = await client.head(url, {
      signal: session.signal,
      headers: baseHeaders,
      validateStatus: (s) => s >= 200 && s < 500,
    });
    if (head.status >= 200 && head.status < 300) {
      return {
        total: parseContentLength(head.headers as Record<string, unknown>),
        supportsRange: parseAcceptRanges(head.headers as Record<string, unknown>),
      };
    }
  } catch {
    // Fall through to Range probe.
  }

  session.throwIfCancelled();

  try {
    const probe = await client.get(url, {
      responseType: 'arraybuffer',
      signal: session.signal,
      headers: { ...(baseHeaders ?? {}), Range: 'bytes=0-0' },
      validateStatus: (s) => s === 200 || s === 206 || (s >= 400 && s < 500),
    });
    if (probe.status === 206) {
      return {
        total: parseContentRangeTotal(probe.headers as Record<string, unknown>),
        supportsRange: true,
      };
    }
    if (probe.status === 200) {
      const len = parseContentLength(probe.headers as Record<string, unknown>);
      // Server ignored Range and returned a body — only reuse if tiny.
      const data = probe.data as ArrayBuffer;
      if (data.byteLength <= 2 && (len == null || len <= 2)) {
        return { total: len, supportsRange: false };
      }
      // Full body already downloaded (small files) — signal via negative supportsRange + total.
      return {
        total: len ?? data.byteLength,
        supportsRange: false,
      };
    }
  } catch {
    /* single-shot fallback */
  }

  return { total: null, supportsRange: false };
}

/**
 * Downloads a URL as Blob with progress, optional Range pause/resume.
 */
export async function fetchBlobProgressive(
  request: ProgressiveDownloadRequest
): Promise<{ blob: Blob; fileName: string; headers: Record<string, unknown> }> {
  const client = request.axiosInstance ?? AXIOS_INSTANCE;
  const chunkSize = request.chunkSizeBytes ?? DOWNLOAD_RESUME_CHUNK_BYTES;
  const speed = new DownloadSpeedTracker();
  const baseHeaders = request.axiosConfig?.headers;

  const baseSnap = (): Omit<
    ProgressiveDownloadSnapshot,
    'phase' | 'loadedBytes' | 'totalBytes' | 'percent' | 'bytesPerSecond' | 'etaSeconds' | 'supportsPause'
  > => ({
    fileName: request.fileName,
    label: request.label,
  });

  emit(request.onProgress, {
    ...baseSnap(),
    phase: 'starting',
    loadedBytes: 0,
    totalBytes: request.expectedTotalBytes ?? null,
    percent: 0,
    bytesPerSecond: 0,
    etaSeconds: null,
    supportsPause: false,
  });

  const report = (
    phase: ProgressiveDownloadPhase,
    loaded: number,
    total: number | null,
    supportsPause: boolean,
    extra?: Partial<ProgressiveDownloadSnapshot>
  ) => {
    const bps = phase === 'downloading' ? speed.update(loaded) : speed.bytesPerSecond();
    const remaining = total != null ? Math.max(0, total - loaded) : 0;
    emit(request.onProgress, {
      ...baseSnap(),
      phase,
      loadedBytes: loaded,
      totalBytes: total,
      percent: clampPercent(loaded, total),
      bytesPerSecond: bps,
      etaSeconds: estimateEtaSeconds(remaining, bps),
      supportsPause,
      ...extra,
    });
  };

  try {
    request.session.throwIfCancelled();
    const probe = await probeRangeSupport(client, request.url, request.session, baseHeaders);
    const total =
      probe.total ??
      (request.expectedTotalBytes != null && request.expectedTotalBytes > 0
        ? request.expectedTotalBytes
        : null);

    if (probe.supportsRange && total != null && total > 0) {
      const parts: ArrayBuffer[] = [];
      let offset = 0;
      let lastHeaders: Record<string, unknown> = {};

      while (offset < total) {
        await request.session.waitWhilePaused();
        request.session.throwIfCancelled();

        if (request.session.isPaused) {
          report('paused', offset, total, true);
          continue;
        }

        report('downloading', offset, total, true);

        const end = Math.min(offset + chunkSize - 1, total - 1);
        const chunkAbort = new AbortController();
        request.session.bindChunkAbort(chunkAbort);

        const onAbort = () => chunkAbort.abort();
        request.session.signal.addEventListener('abort', onAbort);

        try {
          const res = await client.get(request.url, {
            ...request.axiosConfig,
            responseType: 'arraybuffer',
            signal: chunkAbort.signal,
            headers: {
              ...(baseHeaders ?? {}),
              Range: `bytes=${offset}-${end}`,
            },
            validateStatus: (s) => s === 200 || s === 206,
            onDownloadProgress: (e) => {
              if (request.session.isPaused || request.session.isCancelled) return;
              const loaded = offset + (e.loaded ?? 0);
              report('downloading', Math.min(loaded, total), total, true);
            },
          });
          lastHeaders = res.headers as Record<string, unknown>;
          const buf = res.data as ArrayBuffer;
          if (!buf || buf.byteLength === 0) {
            throw new Error('Empty download chunk');
          }
          parts.push(buf);
          offset += buf.byteLength;
          report('downloading', offset, total, true);
        } catch (err) {
          if (request.session.isCancelled) {
            throw new ProgressiveDownloadCancelledError();
          }
          if (request.session.isPaused || axios.isCancel?.(err) || (err as { code?: string })?.code === 'ERR_CANCELED') {
            report('paused', offset, total, true);
            continue;
          }
          throw err;
        } finally {
          request.session.signal.removeEventListener('abort', onAbort);
        }
      }

      const blob = new Blob(parts, { type: 'application/octet-stream' });
      const fileName = parseFilenameFromContentDisposition(
        String(lastHeaders['content-disposition'] ?? lastHeaders['Content-Disposition'] ?? ''),
        request.fileName
      );
      report('done', blob.size, total, true);
      return { blob, fileName, headers: lastHeaders };
    }

    // Single-shot download (no reliable Range).
    report('downloading', 0, total, false);
    const fullAbort = new AbortController();
    request.session.bindChunkAbort(fullAbort);
    const onAbort = () => fullAbort.abort();
    request.session.signal.addEventListener('abort', onAbort);

    try {
      const res = await client.get(request.url, {
        ...request.axiosConfig,
        responseType: 'arraybuffer',
        signal: fullAbort.signal,
        headers: baseHeaders,
        onDownloadProgress: (e) => {
          if (request.session.isCancelled) return;
          const loaded = e.loaded ?? 0;
          const t = e.total && e.total > 0 ? e.total : total;
          report('downloading', loaded, t ?? null, false);
        },
      });
      const buf = res.data as ArrayBuffer;
      const blob = new Blob([buf], {
        type: String(res.headers['content-type'] ?? 'application/octet-stream'),
      });
      const fileName = parseFilenameFromContentDisposition(
        res.headers['content-disposition'] as string | undefined,
        request.fileName
      );
      report('done', blob.size, blob.size, false);
      return { blob, fileName, headers: res.headers as Record<string, unknown> };
    } finally {
      request.session.signal.removeEventListener('abort', onAbort);
    }
  } catch (err) {
    if (
      err instanceof ProgressiveDownloadCancelledError ||
      request.session.isCancelled ||
      (axios.isAxiosError(err) && err.code === 'ERR_CANCELED')
    ) {
      emit(request.onProgress, {
        ...baseSnap(),
        phase: 'cancelled',
        loadedBytes: 0,
        totalBytes: request.expectedTotalBytes ?? null,
        percent: 0,
        bytesPerSecond: 0,
        etaSeconds: null,
        supportsPause: false,
        errorKind: 'cancelled',
      });
      throw new ProgressiveDownloadCancelledError();
    }

    const network = isNetworkDownloadError(err);
    emit(request.onProgress, {
      ...baseSnap(),
      phase: 'error',
      loadedBytes: 0,
      totalBytes: request.expectedTotalBytes ?? null,
      percent: 0,
      bytesPerSecond: 0,
      etaSeconds: null,
      supportsPause: false,
      errorKind: network ? 'network' : 'unknown',
      errorMessage: err instanceof Error ? err.message : String(err),
    });
    throw err;
  }
}
