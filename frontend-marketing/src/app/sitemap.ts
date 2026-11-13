import type { MetadataRoute } from 'next';
import { absoluteUrl, PUBLIC_PATHS } from '@/seo/site';

export default function sitemap(): MetadataRoute.Sitemap {
  return PUBLIC_PATHS.map((path) => ({
    url: absoluteUrl(path),
    changeFrequency: 'weekly',
    priority: path === '/' ? 1 : 0.4,
  }));
}
