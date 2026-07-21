/**
 * Record API latency / success and emit Sentry signals when SLOs breach.
 */

import * as Sentry from '@sentry/nextjs';

import { recordApiMetricSample, getApiMetricsSummary } from '@/lib/monitoring/apiMetricsStore';
import { isSentryActive, captureMessage } from '@/lib/monitoring/reportToSentry';
import { sanitizeApiPath } from '@/lib/monitoring/sanitizeApiPath';
import {
  ALERT_COOLDOWN_MS,
  API_RESPONSE_TIME_ALERT_MS,
  MONITORING_THRESHOLDS,
} from '@/lib/monitoring/thresholds';

export type ApiMetricInput = {
  method?: string;
  url?: string | null;
  status?: number | null;
  durationMs: number;
  ok: boolean;
};

const lastAlertAt = new Map<string, number>();

function shouldEmitAlert(key: string, now = Date.now()): boolean {
  const prev = lastAlertAt.get(key) ?? 0;
  if (now - prev < ALERT_COOLDOWN_MS) {
    return false;
  }
  lastAlertAt.set(key, now);
  return true;
}

function maybeBeaconMetric(payload: Record<string, unknown>): void {
  if (typeof window === 'undefined') {
    return;
  }
  if (process.env.NEXT_PUBLIC_METRICS_BEACON?.trim().toLowerCase() !== 'true') {
    return;
  }
  try {
    const body = JSON.stringify(payload);
    if (typeof navigator !== 'undefined' && typeof navigator.sendBeacon === 'function') {
      navigator.sendBeacon('/api/monitoring/metrics', new Blob([body], { type: 'application/json' }));
      return;
    }
    void fetch('/api/monitoring/metrics', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body,
      keepalive: true,
    }).catch(() => {});
  } catch {
    // ignore
  }
}

export function reportApiMetric(input: ApiMetricInput): void {
  if (typeof window === 'undefined') {
    return;
  }

  const path = sanitizeApiPath(input.url);
  const method = (input.method ?? 'GET').toUpperCase();
  const status = typeof input.status === 'number' ? input.status : 0;
  const durationMs = Math.max(0, Math.round(input.durationMs));
  const ok = input.ok;

  recordApiMetricSample({ durationMs, status, ok, method, path });

  maybeBeaconMetric({
    type: 'api_metric',
    method,
    path,
    status,
    durationMs,
    ok,
    ts: new Date().toISOString(),
  });

  if (!isSentryActive()) {
    evaluateLocalAlertsOnly();
    return;
  }

  try {
    const metrics = (Sentry as unknown as { metrics?: { distribution?: Function; increment?: Function } })
      .metrics;
    metrics?.distribution?.('fa.api.duration', durationMs, {
      unit: 'millisecond',
      attributes: { method, path, ok: String(ok) },
    });
    metrics?.increment?.('fa.api.requests', 1, {
      attributes: { method, path, ok: String(ok), status: String(status) },
    });
    if (!ok) {
      metrics?.increment?.('fa.api.errors', 1, {
        attributes: { method, path, status: String(status) },
      });
    }
  } catch {
    // optional Metrics API
  }

  Sentry.setMeasurement('fa.api.duration', durationMs, 'millisecond');

  if (durationMs > API_RESPONSE_TIME_ALERT_MS && shouldEmitAlert(`slow:${method}:${path}`)) {
    captureMessage(`API response time exceeded ${API_RESPONSE_TIME_ALERT_MS}ms`, {
      level: 'warning',
      tags: {
        source: 'api-metrics',
        alert: 'response_time',
        httpMethod: method,
        path,
      },
      extra: {
        durationMs,
        status,
        thresholdMs: API_RESPONSE_TIME_ALERT_MS,
      },
    });
  }

  const summary = getApiMetricsSummary();
  if (summary.errorRateAlert && shouldEmitAlert('error-rate')) {
    captureMessage(
      `API error rate exceeded ${(MONITORING_THRESHOLDS.apiErrorRate * 100).toFixed(0)}%`,
      {
        level: 'error',
        tags: {
          source: 'api-metrics',
          alert: 'error_rate',
        },
        extra: {
          errorRate: summary.errorRate,
          sampleCount: summary.sampleCount,
          errorCount: summary.errorCount,
          windowMs: summary.windowMs,
          threshold: MONITORING_THRESHOLDS.apiErrorRate,
        },
      },
    );
  }
}

function evaluateLocalAlertsOnly(): void {
  // Store already updated; dashboard reads summary. No Sentry in local/dev.
}
