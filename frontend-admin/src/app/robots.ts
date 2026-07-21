import type { MetadataRoute } from 'next';

/**
 * Admin FA must not be indexed (auth-gated operator UI).
 * Served at `/robots.txt` via App Router.
 */
export default function robots(): MetadataRoute.Robots {
  return {
    rules: {
      userAgent: '*',
      disallow: '/',
    },
  };
}
