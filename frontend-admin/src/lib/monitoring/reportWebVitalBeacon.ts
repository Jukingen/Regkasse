/**
 * Optional same-origin beacon for self-hosted / Grafana log pipelines.
 * Enabled when NEXT_PUBLIC_WEB_VITALS_BEACON=true (build-time).
 */
import type { WebVitalPayload } from '@/lib/monitoring/webVitalsBudgets';

export function isWebVitalsBeaconEnabled(): boolean {
  return process.env.NEXT_PUBLIC_WEB_VITALS_BEACON?.trim().toLowerCase() === 'true';
}

export async function reportWebVitalToBeacon(metric: WebVitalPayload): Promise<void> {
  if (!isWebVitalsBeaconEnabled() || typeof window === 'undefined') {
    return;
  }

  const body = JSON.stringify(metric);
  const url = '/api/monitoring/web-vitals';

  try {
    if (typeof navigator !== 'undefined' && typeof navigator.sendBeacon === 'function') {
      const blob = new Blob([body], { type: 'application/json' });
      const ok = navigator.sendBeacon(url, blob);
      if (ok) {
        return;
      }
    }
    await fetch(url, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body,
      keepalive: true,
      credentials: 'same-origin',
    });
  } catch {
    // Telemetry must never break the UI.
  }
}
