import type { MetadataRoute } from 'next';

/**
 * Empty sitemap — Admin FA is not a public marketing site.
 * `/sitemap.xml` exists so crawlers that request it learn there are no URLs to index.
 * Do not list /admin, /rksv, /backup, or login routes here.
 */
export default function sitemap(): MetadataRoute.Sitemap {
  return [];
}
