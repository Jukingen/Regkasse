/**
 * In-browser rolling window of API call samples for the FA monitoring dashboard.
 * No request bodies, tokens, or query strings.
 */

import {
  API_METRICS_WINDOW_MS,
  MONITORING_THRESHOLDS,
} from '@/lib/monitoring/thresholds';

export type ApiMetricSample = {
  at: number;
  durationMs: number;
  status: number;
  ok: boolean;
  method: string;
  path: string;
};

export type ApiMetricsSummary = {
  windowMs: number;
  sampleCount: number;
  errorCount: number;
  /** 0–1 fraction of failed samples in the window */
  errorRate: number;
  /** true when errorRate exceeds threshold and sampleCount >= min */
  errorRateAlert: boolean;
  p50Ms: number | null;
  p95Ms: number | null;
  p99Ms: number | null;
  slowCount: number;
  /** true when any recent sample exceeded response-time threshold */
  hasSlowRequests: boolean;
  thresholds: typeof MONITORING_THRESHOLDS;
};

const MAX_SAMPLES = 300;
const samples: ApiMetricSample[] = [];
const listeners = new Set<() => void>();

function prune(now = Date.now()): void {
  const cutoff = now - API_METRICS_WINDOW_MS;
  while (samples.length > 0 && samples[0]!.at < cutoff) {
    samples.shift();
  }
  while (samples.length > MAX_SAMPLES) {
    samples.shift();
  }
}

function percentile(sorted: number[], p: number): number | null {
  if (sorted.length === 0) {
    return null;
  }
  const idx = Math.min(sorted.length - 1, Math.max(0, Math.ceil((p / 100) * sorted.length) - 1));
  return sorted[idx] ?? null;
}

export function recordApiMetricSample(sample: Omit<ApiMetricSample, 'at'> & { at?: number }): void {
  if (typeof window === 'undefined') {
    return;
  }
  samples.push({
    at: sample.at ?? Date.now(),
    durationMs: sample.durationMs,
    status: sample.status,
    ok: sample.ok,
    method: sample.method,
    path: sample.path,
  });
  prune();
  listeners.forEach((fn) => {
    try {
      fn();
    } catch {
      // ignore
    }
  });
}

export function getApiMetricSamples(): readonly ApiMetricSample[] {
  prune();
  return samples;
}

export function getApiMetricsSummary(now = Date.now()): ApiMetricsSummary {
  prune(now);
  const windowSamples = samples.filter((s) => s.at >= now - API_METRICS_WINDOW_MS);
  const errorCount = windowSamples.filter((s) => !s.ok).length;
  const sampleCount = windowSamples.length;
  const errorRate = sampleCount === 0 ? 0 : errorCount / sampleCount;
  const durations = windowSamples.map((s) => s.durationMs).sort((a, b) => a - b);
  const slowCount = windowSamples.filter(
    (s) => s.durationMs > MONITORING_THRESHOLDS.apiResponseTimeMs,
  ).length;

  return {
    windowMs: API_METRICS_WINDOW_MS,
    sampleCount,
    errorCount,
    errorRate,
    errorRateAlert:
      sampleCount >= MONITORING_THRESHOLDS.apiErrorRateMinSamples &&
      errorRate > MONITORING_THRESHOLDS.apiErrorRate,
    p50Ms: percentile(durations, 50),
    p95Ms: percentile(durations, 95),
    p99Ms: percentile(durations, 99),
    slowCount,
    hasSlowRequests: slowCount > 0,
    thresholds: MONITORING_THRESHOLDS,
  };
}

/** Subscribe to store updates (dashboard refresh). Returns unsubscribe. */
export function subscribeApiMetrics(listener: () => void): () => void {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}

/** Test helper */
export function __resetApiMetricsStoreForTests(): void {
  samples.length = 0;
  listeners.clear();
}
