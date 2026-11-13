'use client';

/**
 * Collects Core Web Vitals via `web-vitals` and reports to Sentry (+ optional beacon).
 *
 * Metrics: FCP, LCP, CLS, TTFB, INP (INP replaces deprecated TTI).
 */
import { usePathname } from 'next/navigation';
import { useEffect, useRef } from 'react';
import { onCLS, onFCP, onINP, onLCP, onTTFB, type Metric } from 'web-vitals';

import { reportWebVitalToBeacon } from '@/lib/monitoring/reportWebVitalBeacon';
import { reportWebVitalToSentry } from '@/lib/monitoring/reportWebVitalToSentry';
import {
  type WebVitalName,
  type WebVitalPayload,
  sanitizeRoutePath,
} from '@/lib/monitoring/webVitalsBudgets';
import { technicalConsole } from '@/shared/dev/technicalConsole';

function toPayload(metric: Metric, route: string): WebVitalPayload | null {
  const name = metric.name as WebVitalName;
  if (name !== 'CLS' && name !== 'FCP' && name !== 'INP' && name !== 'LCP' && name !== 'TTFB') {
    return null;
  }
  return {
    name,
    value: metric.value,
    rating: metric.rating,
    id: metric.id,
    navigationType: metric.navigationType,
    route,
    delta: metric.delta,
  };
}

/**
 * Mount once under the root layout. Route tags use a ref so client navigations
 * stay accurate without re-subscribing (web-vitals listeners are process-lifetime).
 */
export function WebVitalsReporter() {
  const pathname = usePathname();
  const routeRef = useRef(sanitizeRoutePath(pathname));
  routeRef.current = sanitizeRoutePath(pathname);

  useEffect(() => {
    const handle = (metric: Metric) => {
      const route = routeRef.current;
      const payload = toPayload(metric, route);
      if (!payload) {
        return;
      }

      if (process.env.NODE_ENV === 'development') {
        technicalConsole.devDebug('[web-vitals]', payload.name, payload.value, payload.rating, route);
      }

      reportWebVitalToSentry(payload);
      void reportWebVitalToBeacon(payload);
    };

    onCLS(handle);
    onFCP(handle);
    onINP(handle);
    onLCP(handle);
    onTTFB(handle);
  }, []);

  return null;
}
