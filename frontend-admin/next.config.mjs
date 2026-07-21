/**
 * RKSV / FinanzOnline: üretim derlemesinde ortam etiketi zorunlu (operatör tarafında TEST|PROD ayrımı).
 * Türkçe: Eksik veya geçersiz NEXT_PUBLIC_RKSV_ENVIRONMENT ile sessiz üretim artefaktı üretilmez.
 */
import path from 'node:path';
import { fileURLToPath } from 'node:url';

import bundleAnalyzer from '@next/bundle-analyzer';
import { withSentryConfig } from '@sentry/nextjs';

import { assertRksvPublicEnvironmentForProductionBuild } from './scripts/assertRksvPublicEnvironmentForBuild.mjs';

const __dirname = path.dirname(fileURLToPath(import.meta.url));

assertRksvPublicEnvironmentForProductionBuild();

const withBundleAnalyzer = bundleAnalyzer({
  enabled: process.env.ANALYZE === 'true',
});

const isProd = process.env.NODE_ENV === 'production';
const sentryDsnConfigured = Boolean(
  process.env.NEXT_PUBLIC_SENTRY_DSN?.trim() || process.env.SENTRY_DSN?.trim()
);

/**
 * API origin for CSP `connect-src` / websocket (SignalR, SSE, axios).
 * Falls back to local API when unset (dev).
 */
function resolveApiOrigin() {
  const raw = process.env.NEXT_PUBLIC_API_BASE_URL?.trim();
  if (!raw) {
    return 'http://localhost:5184';
  }
  try {
    return new URL(raw).origin;
  } catch {
    return 'http://localhost:5184';
  }
}

/**
 * Pragmatic CSP for Admin FA.
 * Theme bootstrap + Ant Design need `'unsafe-inline'` styles/scripts; website-preview iframes
 * need broad `frame-src`. Tighten later with nonces if required.
 */
function buildContentSecurityPolicy() {
  const apiOrigin = resolveApiOrigin();
  const wsOrigin = apiOrigin.replace(/^http/i, 'ws');
  const connectExtras = [
    apiOrigin,
    wsOrigin,
    // Local / hosts-file API variants used in Development.
    'http://localhost:5184',
    'http://127.0.0.1:5184',
    'ws://localhost:5184',
    'ws://127.0.0.1:5184',
    // Sentry browser ingest (production monitoring).
    'https://*.ingest.sentry.io',
    'https://*.ingest.de.sentry.io',
    // Vercel Speed Insights (optional; no-op off Vercel).
    'https://vitals.vercel-insights.com',
  ];
  const connectSrc = [`'self'`, ...new Set(connectExtras)].join(' ');

  return [
    "default-src 'self'",
    "base-uri 'self'",
    "object-src 'none'",
    "frame-ancestors 'none'",
    "form-action 'self'",
    // Next.js + inline theme FOUC script; keep eval for toolchain compatibility.
    `script-src 'self' 'unsafe-inline' 'unsafe-eval'`,
    // Ant Design / css-in-js injects inline styles.
    `style-src 'self' 'unsafe-inline'`,
    // Product images, data URLs, blob previews; remotePatterns still gate next/image.
    `img-src 'self' data: blob: https: http://localhost:5184 http://127.0.0.1:5184`,
    `font-src 'self' data:`,
    `connect-src ${connectSrc}`,
    // Tenant website / template preview iframes (http(s) + blob).
    `frame-src 'self' https: http: blob: data:`,
    `worker-src 'self' blob:`,
    "manifest-src 'self'",
  ].join('; ');
}

/** Shared security headers for all routes. */
function buildSecurityHeaders() {
  /** @type {{ key: string; value: string }[]} */
  const headers = [
    { key: 'X-Frame-Options', value: 'DENY' },
    { key: 'X-Content-Type-Options', value: 'nosniff' },
    { key: 'Referrer-Policy', value: 'strict-origin-when-cross-origin' },
    { key: 'X-DNS-Prefetch-Control', value: 'on' },
    {
      key: 'Permissions-Policy',
      value: 'camera=(), microphone=(), geolocation=(), payment=(), usb=(), interest-cohort=()',
    },
    { key: 'Content-Security-Policy', value: buildContentSecurityPolicy() },
  ];

  // HSTS only in production builds — avoid sticky HTTPS on local http://admin.regkasse.local.
  if (isProd) {
    headers.push({
      key: 'Strict-Transport-Security',
      value: 'max-age=63072000; includeSubDomains; preload',
    });
  }

  return headers;
}

/** @type {import('next').NextConfig} */
const nextConfig = {
  allowedDevOrigins: ['admin.regkasse.local', '*.regkasse.local'],
  transpilePackages: [
    'antd',
    '@ant-design/icons',
    'rc-util',
    'rc-pagination',
    'rc-picker',
    'rc-notification',
    'rc-tooltip',
  ],
  reactStrictMode: true,
  // Hide `X-Powered-By: Next.js`
  poweredByHeader: false,
  // SWC minify is always on in Next.js 15+ — do not set deprecated `swcMinify`.
  compress: true,
  // pino uses worker/thread transports — keep it external on the server bundle.
  serverExternalPackages: ['pino', 'thread-stream', 'pino-abstract-transport'],
  compiler: {
    // Strip noisy console.* in production; keep warn/error for ops diagnostics.
    removeConsole: isProd ? { exclude: ['error', 'warn'] } : false,
  },
  // Prefer `remotePatterns` over deprecated `images.domains`.
  images: {
    remotePatterns: [
      { protocol: 'http', hostname: 'localhost', port: '5184', pathname: '/**' },
      { protocol: 'http', hostname: '127.0.0.1', port: '5184', pathname: '/**' },
      { protocol: 'https', hostname: 'api.regkasse.at', pathname: '/**' },
    ],
  },
  turbopack: {
    root: path.join(__dirname, '..'),
  },
  experimental: {
    // Tree-shake heavy icon/component barrels.
    optimizePackageImports: ['antd', '@ant-design/icons'],
  },
  async headers() {
    return [
      {
        source: '/:path*',
        headers: buildSecurityHeaders(),
      },
    ];
  },
  async redirects() {
    return [
      { source: '/sales', destination: '/receipts', permanent: false },
      { source: '/belege', destination: '/receipts', permanent: false },
      { source: '/storno', destination: '/payments/storno-refund-audit', permanent: false },
      { source: '/price-rules', destination: '/pricing-rules', permanent: false },
      {
        source: '/settings/appearance',
        destination: '/settings/personalization',
        permanent: false,
      },
      { source: '/backup/config', destination: '/backup/configuration', permanent: false },
      { source: '/backup/logs', destination: '/backup/audit', permanent: false },
      { source: '/reporting/operational', destination: '/reporting', permanent: false },
      { source: '/reporting/center', destination: '/reporting/report-center', permanent: false },
      {
        source: '/reporting/user-activity',
        destination: '/admin/reports/user-activity',
        permanent: false,
      },
      {
        source: '/rksv/monatsbeleg',
        destination: '/rksv/sonderbelege?focus=monatsbeleg',
        permanent: false,
      },
      {
        source: '/rksv/jahresbeleg',
        destination: '/rksv/sonderbelege?focus=jahresbeleg',
        permanent: false,
      },
      // Virtual Sonderbeleg menu keys + short aliases → canonical focus panels
      {
        source: '/rksv/sb/startbeleg',
        destination: '/rksv/sonderbelege?focus=startbeleg',
        permanent: false,
      },
      {
        source: '/rksv/sb/monatsbeleg',
        destination: '/rksv/sonderbelege?focus=monatsbeleg',
        permanent: false,
      },
      {
        source: '/rksv/sb/monats',
        destination: '/rksv/sonderbelege?focus=monatsbeleg',
        permanent: false,
      },
      {
        source: '/rksv/sb/jahresbeleg',
        destination: '/rksv/sonderbelege?focus=jahresbeleg',
        permanent: false,
      },
      {
        source: '/rksv/sb/jahres',
        destination: '/rksv/sonderbelege?focus=jahresbeleg',
        permanent: false,
      },
      {
        source: '/rksv/sb/nullbeleg',
        destination: '/rksv/sonderbelege?focus=nullbeleg',
        permanent: false,
      },
      {
        source: '/rksv/sb/schlussbeleg',
        destination: '/rksv/sonderbelege?focus=schlussbeleg',
        permanent: false,
      },
      {
        source: '/rksv/sb/test-helper',
        destination: '/rksv/sonderbelege?focus=test-helper',
        permanent: false,
      },
    ];
  },
};

const analyzedConfig = withBundleAnalyzer(nextConfig);

/**
 * Wrap with Sentry build tooling when a DSN is present.
 * Source-map upload runs only when `SENTRY_AUTH_TOKEN` (+ org/project) is set.
 */
export default withSentryConfig(analyzedConfig, {
  org: process.env.SENTRY_ORG || undefined,
  project: process.env.SENTRY_PROJECT || undefined,
  authToken: process.env.SENTRY_AUTH_TOKEN || undefined,
  silent: !process.env.CI,
  // Avoid widening the client surface when monitoring is not configured.
  disableLogger: true,
  widenClientFileUpload: false,
  sourcemaps: {
    disable: !process.env.SENTRY_AUTH_TOKEN,
  },
  // Same-origin tunnel reduces ad-blocker drops for event ingest.
  tunnelRoute: sentryDsnConfigured ? '/monitoring-tunnel' : undefined,
});
