'use client';

/**
 * FA performance monitoring shell:
 * - Vercel Speed Insights (Core Web Vitals on Vercel)
 * - Custom web-vitals reporter → Sentry (+ optional self-hosted beacon)
 */
import { SpeedInsights } from '@vercel/speed-insights/next';

import { WebVitalsReporter } from '@/components/monitoring/WebVitalsReporter';

function isSpeedInsightsEnabled(): boolean {
  const flag = process.env.NEXT_PUBLIC_SPEED_INSIGHTS?.trim().toLowerCase();
  if (flag === 'false' || flag === '0' || flag === 'off') {
    return false;
  }
  // Default on in production builds; off in local/dev unless explicitly enabled.
  if (flag === 'true' || flag === '1' || flag === 'on') {
    return true;
  }
  return process.env.NODE_ENV === 'production';
}

export function PerformanceMonitoring() {
  return (
    <>
      <WebVitalsReporter />
      {isSpeedInsightsEnabled() ? <SpeedInsights sampleRate={1} /> : null}
    </>
  );
}
