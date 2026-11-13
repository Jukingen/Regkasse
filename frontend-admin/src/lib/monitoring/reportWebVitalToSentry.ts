/**
 * Report Core Web Vitals to Sentry (measurements + degradation warnings).
 * No-ops when Sentry is inactive (local/dev without DSN).
 */
import * as Sentry from '@sentry/nextjs';

import { isSentryActive } from '@/lib/monitoring/reportToSentry';
import {
  type WebVitalPayload,
  budgetFor,
  exceedsBudget,
  isTimingVital,
} from '@/lib/monitoring/webVitalsBudgets';

/** Attach the vital to the active span / scope for Performance views. */
export function reportWebVitalToSentry(metric: WebVitalPayload): void {
  if (!isSentryActive()) {
    return;
  }

  const unit = isTimingVital(metric.name) ? 'millisecond' : 'none';
  const measurementName = `web_vital.${metric.name.toLowerCase()}`;

  Sentry.setMeasurement(measurementName, metric.value, unit);

  Sentry.getCurrentScope().setContext('web_vital', {
    name: metric.name,
    value: metric.value,
    rating: metric.rating,
    id: metric.id,
    route: metric.route,
    navigationType: metric.navigationType,
    budget: budgetFor(metric.name),
  });

  // Optional distribution metric (Sentry Metrics) when the SDK supports it.
  try {
    const metrics = (
      Sentry as unknown as {
        metrics?: {
          distribution?: (
            name: string,
            value: number,
            options?: { unit?: string; attributes?: Record<string, string> }
          ) => void;
        };
      }
    ).metrics;
    metrics?.distribution?.(measurementName, metric.value, {
      unit,
      attributes: {
        rating: metric.rating,
        route: metric.route,
      },
    });
  } catch {
    // Metrics API optional — ignore if unavailable.
  }

  if (exceedsBudget(metric.name, metric.value)) {
    Sentry.captureMessage(`Web vital degraded: ${metric.name}`, {
      level: 'warning',
      tags: {
        source: 'web-vitals',
        vital: metric.name,
        rating: metric.rating,
        route: metric.route,
      },
      extra: {
        value: metric.value,
        budget: budgetFor(metric.name),
        id: metric.id,
        navigationType: metric.navigationType,
      },
    });
  }
}
