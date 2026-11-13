import type { MetadataRoute } from 'next';

/**
 * Web app manifest for optional “Add to Home Screen” on mobile.
 * Admin FA is not an offline PWA — no service worker (avoid caching fiscal/admin data).
 * Served as `/manifest.webmanifest` by the App Router.
 */
export default function manifest(): MetadataRoute.Manifest {
  return {
    name: 'Regkasse Admin',
    short_name: 'Regkasse',
    description: 'Admin panel for Regkasse POS (operators / Mandanten-Admin).',
    start_url: '/dashboard',
    scope: '/',
    display: 'standalone',
    orientation: 'any',
    lang: 'de',
    dir: 'ltr',
    theme_color: '#1677ff',
    background_color: '#ffffff',
    categories: ['business', 'finance', 'productivity'],
    icons: [
      {
        src: '/icon-192x192.png',
        sizes: '192x192',
        type: 'image/png',
        purpose: 'any',
      },
      {
        src: '/icon-512x512.png',
        sizes: '512x512',
        type: 'image/png',
        purpose: 'any',
      },
      {
        src: '/icon-512x512-maskable.png',
        sizes: '512x512',
        type: 'image/png',
        purpose: 'maskable',
      },
    ],
  };
}
