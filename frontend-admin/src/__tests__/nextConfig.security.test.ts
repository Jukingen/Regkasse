import { describe, expect, it } from 'vitest';

/**
 * Lightweight contract checks for security-oriented next.config behaviour.
 * Imports the config module (safe under vitest — RKSV assert only runs for `next build`).
 */
describe('next.config security headers', () => {
  it('exposes security headers including CSP and clickjacking defenses', async () => {
    const mod = await import('../../next.config.mjs');
    const config = mod.default;
    expect(config.poweredByHeader).toBe(false);
    expect(typeof config.headers).toBe('function');

    const rules = await config.headers();
    expect(rules).toHaveLength(1);
    const headers = Object.fromEntries(rules[0].headers.map((h) => [h.key, h.value]));

    expect(headers['X-Frame-Options']).toBe('DENY');
    expect(headers['X-Content-Type-Options']).toBe('nosniff');
    expect(headers['Referrer-Policy']).toBe('strict-origin-when-cross-origin');
    expect(headers['Content-Security-Policy']).toContain("frame-ancestors 'none'");
    expect(headers['Content-Security-Policy']).toContain("default-src 'self'");
    expect(headers['Content-Security-Policy']).toContain('https://*.ingest.sentry.io');
    expect(headers['Content-Security-Policy']).toContain('https://vitals.vercel-insights.com');
    expect(headers['Permissions-Policy']).toContain('camera=()');

    // Vitest runs with NODE_ENV=test — HSTS must not stick on local http.
    expect(headers['Strict-Transport-Security']).toBeUndefined();
  });

  it('uses remotePatterns instead of deprecated images.domains', async () => {
    const mod = await import('../../next.config.mjs');
    const images = mod.default.images;
    expect(images.domains).toBeUndefined();
    expect(images.remotePatterns?.some((p) => p.hostname === 'api.regkasse.at')).toBe(true);
  });
});
